using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Faker.End2End.Tests
{
    public class NullableClass
    {
        public double? D { get; set; }

        public string S { get; set; }

        public int? I { get; set; }

        public DateTime? Dt { get; set; }

        public TimeSpan? Ts { get; set; }

        public IEnumerable<int> Ints { get; set; }

        public float? F { get; set; }

        public long? L { get; set; }

        public Subclass Sub { get; set; }
    }

    public class Subclass
    {
        public int? I2 { get; set; }

        public double? D2 { get; set; }
    }

    [TestFixture]
    public class NullableClassesSpec
    {
        private Fake<NullableClass> _nullableFake = new Fake<NullableClass>(true, 0.1d);

        [Test]
        public void ShouldGenerateNullableFields()
        {
            var fakes = _nullableFake.Generate(100);

            Assert.True(fakes.Any(x => x.D == null));
            Assert.True(fakes.Any(x => x.Ints == null));
            Assert.True(fakes.Any(x => x.S == null));
            Assert.True(fakes.Any(x => x.Sub == null));
            Assert.True(fakes.Any(x => x.Dt == null));
        }
    }
}

