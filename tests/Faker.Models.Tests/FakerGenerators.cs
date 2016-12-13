using System;
using Faker.Helpers;
using Faker.Selectors;
using FsCheck;

namespace Faker.Models.Tests
{
    /// <summary>
    /// Generator class used to create FsCheck data
    /// </summary>
    public class FakerGenerators
    {
        private static readonly ITypeSelector[] AvailableSelectors = new ITypeSelector[]
        {
            new IntSelector(), new GuidSelector(), new StringSelector(), new TimeStampSelector(),
            new DoubleSelector(), new DateTimeSelector(), new EmailSelector(), new FirstNameSelector(),
            new LastNameSelector(), new DecimalSelector(),
        };

        public static Arbitrary<ITypeSelector> Selector()
        {
            return Arb.From(Gen.Elements(AvailableSelectors));
        }

        //public static Arbitrary<ITypeSelector[]> Selectors()
        //{
        //    return Gen.Sequence(Gen.Elements(AvailableSelectors)).ToArbitrary();
        //}

        public static Arbitrary<TypeTable> TypeTable()
        {
            Func<TypeTable> generator = () =>
            {
                var table = new TypeTable(Faker.Generators.Booleans.Bool());
                var selectors = AvailableSelectors.GetRandomSelection(Generators.Numbers.Int(0, 10));
                foreach(var selector in selectors)
                    table.AddSelector(selector);
                return table;
            };
            
            return Arb.From(Gen.Fresh(generator));
        }
    }
}