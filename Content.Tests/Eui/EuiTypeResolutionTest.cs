// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client._DV.CosmicCult.UI;
using Content.Client.Eui;
using Content.Client.Revolutionary.UI;
using NUnit.Framework;

namespace Content.Tests.Eui
{
    [TestFixture]
    [TestOf(typeof(EuiManager))]
    public sealed class EuiTypeResolutionTest
    {
        /// <summary>
        /// The server only sends <see cref="Type.Name"/> of its EUI, and the old client code resolved it with
        /// a suffix match. "CosmicDeconvertedEui" ends with "DeconvertedEui", so the cosmic cult window could be
        /// opened instead of the revolutionary one. An exact name match must always win.
        /// </summary>
        [Test]
        public void ExactNameMatchWinsOverSuffixMatch()
        {
            var types = GetClientTypes();

            // Document the trap that caused the bug.
            Assert.That(typeof(CosmicDeconvertedEui).Name, Does.EndWith(nameof(DeconvertedEui)),
                "CosmicDeconvertedEui no longer shadows DeconvertedEui, the regression test below can be revisited.");

            var looseMatch = types.FirstOrDefault(type =>
                type.FullName != null && type.FullName.EndsWith(nameof(DeconvertedEui)));
            TestContext.WriteLine($"Loose (old) lookup would return: {looseMatch?.FullName ?? "<none>"}");

            var resolved = EuiManager.FindExactEuiType(nameof(DeconvertedEui), types);

            Assert.That(resolved, Is.EqualTo(typeof(DeconvertedEui)));
        }

        [Test]
        public void CosmicCultEuiStillResolves()
        {
            var resolved = EuiManager.FindExactEuiType(nameof(CosmicDeconvertedEui), GetClientTypes());

            Assert.That(resolved, Is.EqualTo(typeof(CosmicDeconvertedEui)));
        }

        [Test]
        public void UnknownNameFallsBackToLooseLookup()
        {
            Assert.That(EuiManager.FindExactEuiType("NotAnEui", GetClientTypes()), Is.Null);
        }

        private static IReadOnlyList<Type> GetClientTypes()
        {
            try
            {
                return typeof(EuiManager).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(type => type != null).ToList()!;
            }
        }
    }
}
