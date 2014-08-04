using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PubComp.Building.NuGetPack.UnitTests
{
    public static class LinqAssert
    {
        public static void Any<TEntity>(IEnumerable<TEntity> collection, Func<TEntity, bool> predicate, String conditionDescription = null)
        {
            Assert.IsTrue(
                collection.Any(predicate),
                "Collection does not contain a matching item" + (string.IsNullOrEmpty(conditionDescription) ? string.Empty : ": " + conditionDescription));
        }

        public static void All<TEntity>(IEnumerable<TEntity> collection, Func<TEntity, bool> predicate, String conditionDescription = null)
        {
            Assert.IsTrue(
                collection.All(predicate),
                "Collection contains a non-matching item" + (string.IsNullOrEmpty(conditionDescription) ? string.Empty : ": " + conditionDescription));
        }

        public static void Single<TEntity>(IEnumerable<TEntity> collection, Func<TEntity, bool> predicate, String conditionDescription = null)
        {
            var predicateCount = collection.Count(predicate);

            Assert.AreEqual(1, predicateCount,
                "Collection does not contain a single item (found " + predicateCount + ")"
                    + (string.IsNullOrEmpty(conditionDescription) ? string.Empty : ": " + conditionDescription));
        }

        public static void Count<TEntity>(IEnumerable<TEntity> collection, int expectedCount)
        {
            var actualCount = collection.Count();
            Assert.AreEqual(expectedCount, actualCount,
                "Collection expectedCount expected: " + expectedCount + ", actual: " + actualCount);
        }
    }
}
