using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Faker.Generators;
using Faker.Helpers;
using Faker.Selectors;

namespace Faker
{
    /// <summary>
    ///     Class used to match a type selector from the TypeTable with the properties of an object
    /// </summary>
    public class Matcher
    {
        /// <summary>
        /// The probability that a null value will be generated for arrays, subclasses, and anything
        /// not explicitly chosen by a <see cref="ITypeSelector"/>.
        /// </summary>
        public double NullProbability { get; private set; }

        /// <summary>
        /// Whether or not we allow nulls at all, as determined by <see cref="NullProbability"/>
        /// </summary>
        public bool AllowNulls => NullProbability > 0.0d;

        /// <summary>
        ///     Default constructor - uses the default TypeTable
        /// </summary>
        public Matcher(double nullProbability = SelectorConstants.NoNullProbability) : this(new TypeTable(nullProbability: nullProbability))
        {
        }

        /// <summary>
        ///     Constructor which accepts a TypeTable as an argument
        /// </summary>
        /// <param name="table">an instantiated type table that can be accessed via the TypeMap property later</param>
        public Matcher(TypeTable table)
        {
            TypeMap = table;
            NullProbability = TypeMap.NullProbability;
        }

        /// <summary>
        /// The set of types and their selectors that are used to generate
        /// random test data.
        /// </summary>
        public TypeTable TypeMap { get; protected set; }

        /// <summary>
        ///     Method matches all properties on a given class with a
        /// </summary>
        /// <typeparam name="T">a class with a parameterless constructor (POCO class)</typeparam>
        /// <param name="targetObject">an instance of the class</param>
        /// <returns>The instance whose value has replaced.</returns>
        public virtual T Match<T>(T targetObject)
        {
            //Check to see if we have a TypeSelector that matches the entire object wholesale first

            //If we don't have a mapper for the wholesale class, map the properties and bind them individually
            var isMatched = false;
            var generatedObject = (T)MapFromSelector(targetObject, typeof(T), out isMatched);
            if (!isMatched)
            {
                //Get all of the properties of the class
                var properties = typeof(T).GetProperties();

                ProcessProperties(properties, targetObject);
            }

            return generatedObject;
        }

        /// <summary>
        ///     Used for matching value types
        /// </summary>
        /// <typeparam name="S">A value type parameter</typeparam>
        /// <param name="targetStruct">The value type instance</param>
        public virtual void MatchStruct<S>(ref S targetStruct)
        {
            //Evaluate all of the possible selectors and find the first available match
            var selector = EvaluateSelectors(typeof(S), TypeMap.GetSelectors(typeof(S)));

            //We found a matching selector
            if (!(selector is MissingSelector))
            {
                var structObject = (object)targetStruct;
                selector.Generate(ref structObject); //Bind the object's value directly
                targetStruct = (S)structObject;
            }
            else
            {
                //Get all of the properties of the class
                var properties = typeof(S).GetProperties();

                targetStruct = (S)ProcessProperties(properties, targetStruct);
            }
        }

        /// <summary>
        ///     Method for iterating over all of the indivdual properties in for a given object
        /// </summary>
        /// <param name="properties">The set of properties available to an object instance</param>
        /// <param name="targetObject">The object against which type selectors will inject values</param>
        protected virtual object ProcessProperties(PropertyInfo[] properties, object targetObject)
        {
            //Iterate over the properties
            foreach (var property in properties)
            {
                if (!property.CanWrite) //Bail if we can't write to the property
                    continue;

                if (property.PropertyType == targetObject.GetType())
                    //If the property is a tree structure, bail (causes infinite recursion otherwise)
                    continue;

                ProcessProperty(property, targetObject);
            }

            return targetObject;
        }

        /// <summary>
        /// Going to use the simplest (fewest arguments) constructor to create the underlying object - so long as it's public.
        /// </summary>
        /// <param name="objectType">The type of object we need to create</param>
        /// <returns>The constructor we're going to use, or NULL if none are found.</returns>
        internal static ConstructorInfo GetSimplestConstructor(Type objectType)
        {
            var publicConstructors = objectType.GetConstructors();
            return publicConstructors.Length == 0 ? publicConstructors.FirstOrDefault() : publicConstructors.OrderBy(x => x.GetParameters().Length).First();
        }

        /// <summary>
        ///     Protected method used to implement our selector-matching strategy. Uses a greedy approach.
        /// </summary>
        /// <param name="property">The meta-data about the property for which we will be finding a match</param>
        /// <param name="targetObject">The object which will receive the property injection</param>
        protected virtual void ProcessProperty(PropertyInfo property, object targetObject)
        {
            //Get the type of the property
            var propertyType = property.PropertyType;

            if (MapFromSelector(property, targetObject, propertyType)) return; //Exit

            //Check to see if the type is a class or struct (no generic type definitions or arrays)
            if (((propertyType.IsClass) ||
                 propertyType.IsValueType) && !IsArray(propertyType))
            {
                var subProperties = propertyType.GetProperties();

                //Create an instance of the underlying subclass
                var subClassInstance = SafeObjectCreate(propertyType);

                //Match all of the properties on the subclass 
                ProcessProperties(subProperties, subClassInstance);

                //Bind the sub-class back onto the original target object
                property.SetValue(targetObject, subClassInstance, null);

                return; //Exit
            }

            //Check to see if the type is an array or any other sort of collection
            if (IsArray(propertyType))
            {
                var arrayInstance = CreateArrayInstance(propertyType, targetObject.GetType());

                //Bind the sub-class back onto the original target object
                property.SetValue(targetObject, arrayInstance, null);
            }
        }

        private static readonly Type FakeType = typeof(Fake<>);

        protected IFake CreateSelectorForType(Type property)
        {
            var fakeType = GenericHelper.CreateGeneric(FakeType, property, AllowNulls, NullProbability);
        }

        private IList CreateArrayInstance(Type propertyType, Type targetType = null)
        {
            //Get the underlying type used int he array
            //var elementType = propertyType.GetElementType(); //Works only for arrays
            var elementType = propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : propertyType.GetElementType(); //Works for IList<T> / IEnumerable<T>

            //Get a number of elements we want to create 
            //Note: (between 1 and 10 for now)
            var elementCount = Numbers.Int(1, 10);

            //Create an instance of our target array
            IList arrayInstance = null;

            //If we're working with a generic list or any other sort of collection
            if (propertyType.IsGenericTypeDefinition)
            {
                arrayInstance = (IList)GenericHelper.CreateGeneric(propertyType, elementType);
            }
            else
            {
                arrayInstance = (IList)GenericHelper.CreateGeneric(typeof(List<>), elementType);
            }

            //Determine if there's a selector available for this type
            var hasSelector = TypeMap.CountSelectors(elementType) > 0;
            ITypeSelector selector = null;

            //So we have a type available for this selector..
            if (hasSelector)
            {
                selector = TypeMap.GetBaseSelector(elementType);
            }

            //If the element in the array isn't the same type as the parent object (recursive objects, like trees)
            if (elementType != targetType)
            {
                for (var i = 0; i < elementCount; i++)
                {
                    //Create a new element instance
                    var element = SafeObjectCreate(elementType);

                    if (hasSelector)
                    {
                        selector.Generate(ref element);
                    }

                    //If the element type is a class populate it recursively
                    else if (elementType.IsClass)
                    {
                        var subProperties = elementType.GetProperties();

                        //Populate all of the properties on this object
                        ProcessProperties(subProperties, element);
                    }

                    arrayInstance.Add(element);
                }
            }
            return arrayInstance;
        }

        /// <summary>
        ///     Attempt to map the object directly based on the availability of selectors
        /// </summary>
        /// <param name="property">The property that needs a match</param>
        /// <param name="targetObject">The object to which the property belongs</param>
        /// <param name="propertyType">The type of the property</param>
        /// <returns>True if a match was found and made; false otherwise</returns>
        protected virtual bool MapFromSelector(PropertyInfo property, object targetObject, Type propertyType)
        {
            //Determine if we have a selector-on-hand for this data type
            var selectorCount = TypeMap.CountSelectors(propertyType);

            //We have some matching selectors, so we'll evaluate and return the best match
            if (selectorCount > 0)
            {
                //Evaluate all of the possible selectors and find the first available match
                var selector = EvaluateSelectors(property, TypeMap.GetSelectors(propertyType));

                //We found a matching selector
                if (!(selector is MissingSelector))
                {
                    selector.Generate(targetObject, property); //Bind the property
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///     Selector-based mapping for when we need to apply it directly to objects wholesale
        /// </summary>
        /// <param name="targetObject">The target object who's value will be replaced</param>
        /// <param name="propertyType">The type of the object</param>
        /// <param name="isMatched">true if a match was made and bound successfully; false otherwise</param>
        /// <returns>The instance whose value has replaced.</returns>
        protected virtual object MapFromSelector(object targetObject, Type propertyType, out bool isMatched)
        {
            //Determine if we have a selector-on-hand for this data type
            var selectorCount = TypeMap.CountSelectors(propertyType);

            //We have some matching selectors, so we'll evaluate and return the best match
            if (selectorCount > 0)
            {
                //Evaluate all of the possible selectors and find the first available match
                var selector = EvaluateSelectors(propertyType, TypeMap.GetSelectors(propertyType));

                //We found a matching selector
                if (!(selector is MissingSelector))
                {
                    selector.Generate(ref targetObject); //Bind the object's value directly
                    isMatched = true;
                    return targetObject;
                }
            }
            isMatched = false;
            return targetObject;
        }

        /// <summary>
        ///     Returns true if the targeted type is an array of some sort
        /// </summary>
        /// <param name="targetType">the type we want to test</param>
        /// <returns>true if it's an array, false otherwise</returns>
        protected virtual bool IsArray(Type targetType)
        {
            if (targetType.IsArray) return true;
            if (!targetType.IsGenericType)
                return false;
            var genericArguments = targetType.GetGenericArguments();
            if (genericArguments.Length != 1)
                return false;

            var listType = typeof(IList<>).MakeGenericType(genericArguments);
            return listType.IsAssignableFrom(targetType);
        }

        /// <summary>
        ///     Method used for safely creating new instances of type objects; handles a few special cases
        ///     where activation has to be done carefully.
        /// </summary>
        /// <param name="t">The target type we want to instantiate</param>
        /// <returns>an instance of the specified type</returns>
        public object SafeObjectCreate(Type t)
        {

            if (TypeMap.CountSelectors(t) > 0)
                return TypeMap.GetSelectors(t).First().GenerateInstance();

            //If the object is a string (tricky)
            if (t == typeof(string))
            {
                return string.Empty;
            }

            // Guids are also tricky
            if (t == typeof (Guid))
                return default(Guid);

            if (IsArray(t))
            {
                return CreateArrayInstance(t);
            }

            object instance = null;
            var constructor = GetSimplestConstructor(t);
            var parameters = constructor?.GetParameters();
            if (constructor == null || parameters.Length == 0)
                instance = Activator.CreateInstance(t);
            else // if we have constructor arguments, recursively create the objects that have them
            {
                var paramInstances = new object[parameters.Length];
                for (var i = 0; i < paramInstances.Length; i++)
                {
                    var paramType = parameters[i];
                    if (paramType.HasDefaultValue) //use the default value if one is defined
                    {
                        paramInstances[i] = paramType.DefaultValue;
                        continue;
                    }

                    var paramInstance = SafeObjectCreate(paramType.ParameterType);
                    //Check to see if T is a value type and attempt to match that
                    if (paramType.ParameterType.IsValueType)
                    {
                        MatchStruct(ref paramInstance);
                    }
                    else
                    {
                        //Match all of the properties of the object and come up with the most reasonable guess we can as to the type of data needed
                        paramInstance = Match(paramInstance);
                    }
                    paramInstances[i] = paramInstance;
                }

                try
                {
                    instance = Activator.CreateInstance(t, paramInstances);
                }
                catch // had an exception - recovering from it and returning a default
                {
                    instance = t.IsValueType ? Activator.CreateInstance(t) : null;
                }
            }

            return instance;
        }

        /// <summary>
        ///     Evaluates a set of selectors and grabs the first available match
        /// </summary>
        /// <param name="propertyType">The Property / Field for which we're trying to find a match</param>
        /// <param name="selectors">A list of selectors from the TypeTable</param>
        /// <returns>the first matching ITypeSelector instance we could find</returns>
        internal virtual ITypeSelector EvaluateSelectors(PropertyInfo propertyType, IEnumerable<ITypeSelector> selectors)
        {
            foreach (var selector in selectors)
            {
                //If the selector can bind
                if (selector.CanBind(propertyType))
                {
                    //Return it
                    return selector;
                }
            }

            //Otherwise, return a MissingSelector and let them know that we can't do it.
            return new MissingSelector();
        }

        /// <summary>
        ///     Evaluates a set of selectors and grabs the first available match
        /// </summary>
        /// <param name="type">The type for which we're trying to find a match</param>
        /// <param name="selectors">A list of selectors from the TypeTable</param>
        /// <returns>the first matching ITypeSelector instance we could find</returns>
        internal virtual ITypeSelector EvaluateSelectors(Type type, IEnumerable<ITypeSelector> selectors)
        {
            foreach (var selector in selectors)
            {
                //If the selector can bind
                if (selector.CanBind(type))
                {
                    //Return it
                    return selector;
                }
            }

            //Otherwise, return a MissingSelector and let them know that we can't do it.
            return new MissingSelector();
        }
    }
}