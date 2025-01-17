using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Testing.Mocking
{
    /// <summary>
    /// Helper class for setting up a mock DbSet for testing.
    /// </summary>
    public class MockDbSet
    {
        /// <summary>
        /// Sets up a mock DbSet to implement IQueryable for testing.
        /// </summary>
        /// <typeparam name="T">
        /// The <see cref="EntityBase"/> implementation type for the DbSet.
        /// </typeparam>
        /// <param name="mockDbSet">The <see cref="Mock{DbSet}"/> to prepare for unit testing.</param>
        /// <param name="backingData">The backing data to use for the DbSet in the unit test.</param>
        public static void SetupMockDbSet<T>(Mock<DbSet<T>> mockDbSet, IQueryable<T> backingData) where T : EntityBase
        {
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(backingData.Provider);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(backingData.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(backingData.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(backingData.GetEnumerator());
        }
    }
}
