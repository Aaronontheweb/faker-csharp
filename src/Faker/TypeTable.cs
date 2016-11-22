using System;
using System.Collections.Generic;
using System.Linq;
using Faker.Helpers;
using Faker.Selectors;

namespace Faker
{
    public enum SelectorPosition
    {
        First,
        Last
    }

    /// <summary>
    ///     The repository which determines the order in which selectors are picked for any given type
    /// </summary>
    public sealed class TypeTable
    {
        //Our internal map for all types and their selectors used within Faker
        private readonly Dictionary<Type, LinkedList<ITypeSelector>> _typeMap;

        /// <summary>
        /// For testing purposes - exposes the underlying typemap
        /// </summary>
        internal IDictionary<Type, LinkedList<ITypeSelector>> TypeMap => _typeMap;

        /// <summary>
        /// The probability that a null value will be used by any of the built-in selectors.
        /// </summary>
        public double NullProbability { get; }

        /// <summary>
        ///     Default constructor
        /// </summary>
        public TypeTable(bool useDefaults = true, double nullProbability = SelectorConstants.NoNullProbability) 
            : this(useDefaults, new Dictionary<Type, LinkedList<ITypeSelector>>(), nullProbability)
        {
        }

        /// <summary>
        ///     Private constructor which accepts an inbound typemap as an argument
        /// </summary>
        /// <param name="useDefaults">Determines if we should load all of our default selectors or not</param>
        /// <param name="typeMap">A typemap instance</param>
        /// <param name="nullProbability">The probability of generating a <c>null</c> value for any built-in, nullable type</param>
        private TypeTable(bool useDefaults, Dictionary<Type, LinkedList<ITypeSelector>> typeMap, double nullProbability)
        {
            _typeMap = typeMap;
            NullProbability = nullProbability;
            if (useDefaults) //Load defaults if the user requested it
                LoadDefaults(nullProbability);
        }

        /// <summary>
        ///     Private internal method for loading all of the default TypeSelectors into the TypeTable
        /// </summary>
        private void LoadDefaults(double nullProbability)
        {
            /* NUMERIC DEFAULT SELECTORS */
            AddSelector(new IntSelector());
            AddSelector(new IntSelector().Nullable(nullProbability));
            AddSelector(new LongSelector());
            AddSelector(new LongSelector().Nullable(nullProbability));
            AddSelector(new TimeStampSelector()); //TimeStamp selector should get used before the long selector
            AddSelector(new TimeStampSelector().Nullable(nullProbability));
            AddSelector(new DoubleSelector());
            AddSelector(new DoubleSelector().Nullable(nullProbability));
            AddSelector(new FloatSelector());
            AddSelector(new FloatSelector().Nullable(nullProbability));
            AddSelector(new DecimalSelector());
            AddSelector(new DecimalSelector().Nullable(nullProbability));

            /* DATETIME SELECTORS */
            AddSelector(new DateTimeSelector());
            AddSelector(new DateTimeSelector().Nullable(nullProbability));
            AddSelector(new DateTimeOffsetSelector());
            AddSelector(new DateTimeOffsetSelector().Nullable(nullProbability));

            /* STRING SELECTORS */
            AddSelector(new StringSelector().Nullable(nullProbability)); //String selector is the very last one we want to try and use
            AddSelector(new FirstNameSelector().Nullable(nullProbability));
            AddSelector(new LastNameSelector().Nullable(nullProbability));
            AddSelector(new FullNameSelector().Nullable(nullProbability));
            AddSelector(new EmailSelector().Nullable(nullProbability));

            /* GUID SELECTORS */
            AddSelector(new GuidSelector());
            AddSelector(new GuidSelector().Nullable());
        }

        /// <summary>
        ///     Internal method for securely creating type rows where needed
        /// </summary>
        /// <param name="incomingType">The type to which we're mapping this object</param>
        private void CreateTypeIfNotExists(Type incomingType)
        {
            //If we already have a collection in our dictionary that corresponds to this type, bail out
            if (_typeMap.ContainsKey(incomingType))
                return;

            //Otherwise, create the row in our dictionary
            _typeMap.Add(incomingType, new LinkedList<ITypeSelector>());
        }

        /// <summary>
        ///     Add a strongly typed selector to the
        /// </summary>
        /// <param name="selector">A ITypeSelector implementation for Type T</param>
        /// <param name="position">
        ///     Optional parameter - indicates whether you want this type selector to be used first or last in
        ///     the sequence of available selectors for this type
        /// </param>
        public void AddSelector(ITypeSelector selector, SelectorPosition position = SelectorPosition.First)
        {
            var activeType = selector.TargetType;
            CreateTypeIfNotExists(activeType);

            if (position == SelectorPosition.First)
                //If the user wants to add this selector to the front of the list (default), do that
                _typeMap[activeType].AddFirst(selector);
            else //Otherwise, add this type to the back of the list
                _typeMap[activeType].AddLast(selector);
        }

        /// <summary>
        ///     Add a strongly typed selector to the
        /// </summary>
        /// <typeparam name="T">The type for which this selector is used</typeparam>
        /// <param name="selector">A TypeSelectorBase implementation for Type T</param>
        /// <param name="position">
        ///     Optional parameter - indicates whether you want this type selector to be used first or last in
        ///     the sequence of available selectors for this type
        /// </param>
        public void AddSelector<T>(TypeSelectorBase<T> selector, SelectorPosition position = SelectorPosition.First)
        {
            AddSelector((ITypeSelector) selector, position);
        }

        /// <summary>
        ///     Remove all of the selectors for a given type
        /// </summary>
        /// <typeparam name="T">The type for which we want to clear all of the selectors</typeparam>
        public void ClearSelectors<T>()
        {
            var activeType = typeof(T);
            if (_typeMap.ContainsKey(activeType)) //Reassign the value to a new list
                _typeMap[activeType] = new LinkedList<ITypeSelector>();
        }

        /// <summary>
        ///     Count all of the selectors for a given type
        /// </summary>
        /// <typeparam name="T">The type for which we need value selectors</typeparam>
        /// <returns>The number of selectors we have available for this type</returns>
        public int CountSelectors<T>()
        {
            var activeType = typeof(T);
            return CountSelectors(activeType);
        }

        /// <summary>
        ///     Count all of the selectors for a given type
        /// </summary>
        /// <param name="t">A type object for which we want to know the number of available selectors</param>
        /// <returns>The number of selectors we have available for this type</returns>
        public int CountSelectors(Type t)
        {
            CreateTypeIfNotExists(t);
            return _typeMap[t].Count;
        }

        /// <summary>
        ///     Get all of the selectors for a given type
        /// </summary>
        /// <typeparam name="T">The type for which we need value selectors</typeparam>
        /// <returns>An enumerable list of selectors</returns>
        public IEnumerable<TypeSelectorBase<T>> GetSelectors<T>()
        {
            var activeType = typeof(T);
            var selectors = GetSelectors(activeType);
            return selectors.Cast<TypeSelectorBase<T>>();
        }

        /// <summary>
        ///     Get all of the selectors for a given type
        /// </summary>
        /// <returns>An enumerable list of selectors</returns>
        public IEnumerable<ITypeSelector> GetSelectors(Type t)
        {
            CreateTypeIfNotExists(t);
            return _typeMap[t];
        }

        /// <summary>
        ///     Gets the base selector for a given datatype
        /// </summary>
        /// <param name="t">The type that we need to inject</param>
        /// <returns>A matching selector, null otherwise</returns>
        public ITypeSelector GetBaseSelector(Type t)
        {
            CreateTypeIfNotExists(t);
            var baseType = GenericHelper.GetGenericType(typeof(PrimitiveSelectorBase<>), t);
            return _typeMap[t].FirstOrDefault(x => baseType.IsAssignableFrom(x.GetType()));
        }

        /// <summary>
        /// Clone a deep copy of <see cref="TypeTable"/> with the same settings as this instance.
        /// 
        /// Any modifications made to the new instance returned should not affect the parent instance.
        /// </summary>
        /// <returns>A deep copy of the current <see cref="TypeTable"/>.</returns>
        public TypeTable Clone()
        {
            // create a deep copy
            var newTypeMap = new Dictionary<Type, LinkedList<ITypeSelector>>();
            foreach (var pair in _typeMap)
            {
                var newList = new ITypeSelector[pair.Value.Count];
                pair.Value.CopyTo(newList, 0);
                newTypeMap.Add(pair.Key, new LinkedList<ITypeSelector>(newList));
            }
            return new TypeTable(false, newTypeMap, NullProbability);
        }
    }
}