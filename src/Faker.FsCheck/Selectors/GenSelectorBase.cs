using System.Dynamic;
using Faker.Selectors;
using FsCheck;

namespace Faker.FsCheck.Selectors
{
    /// <summary>
    /// Base class for <see cref="TypeSelectorBase{T}"/>s that need to use <see cref="Gen"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class GenSelectorBase<T> : TypeSelectorBase<T>
    {
        protected readonly Gen<T> G;

        protected GenSelectorBase(Gen<T> g)
        {
            G = g;
        }

        //public override T Generate()
        //{
        //    Gen.Sized(i => G.Eval())
        //}
    }
}
