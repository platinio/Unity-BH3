using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Two conventions about reading a port that no compiler can enforce and no behaviour test can reach.
    ///
    /// <para>
    /// Both defects behind finding 2.1 are invisible at runtime <em>until the wrong wire exists</em>: a raw
    /// <c>(float)</c> unbox is perfectly fine until someone connects an int, and a node reading its own
    /// float port as <c>int</c> is fine until the value has a fractional part. A behaviour test can only
    /// catch them by guessing which port a future author will get wrong. Reading the source catches all of
    /// them at once, including in nodes nobody has written yet.
    /// </para>
    ///
    /// <para>
    /// Source scanning is unusual, and it is used here because the rules being checked really are textual —
    /// "call this method, not that cast". It is deliberately narrow: it matches the one-line forms this
    /// codebase actually uses, ignores anything it cannot parse, and asserts it found a plausible number of
    /// matches so it can never pass by quietly checking nothing.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PortReadConventionTests
    {
        /// <summary><c>(float) port.GetValue()</c> and <c>port.GetValue() as Transform</c>.</summary>
        private static readonly Regex RawCast = new Regex(
            @"\(\s*[\w\.]+\s*\)\s*\w+\.GetValue\(\)|\w+\.GetValue\(\)\s+as\s+[\w\.]+");

        /// <summary><c>Speed = ValueInput&lt;float&gt;(nameof(Speed)</c> — the declaration of a port's type.</summary>
        private static readonly Regex Declaration = new Regex(
            @"(\w+)\s*=\s*ValueInput<([\w\.]+)>\s*\(\s*nameof\(\s*\1\s*\)");

        /// <summary><c>Speed.GetValue&lt;float&gt;()</c> — a typed read of a port.</summary>
        private static readonly Regex TypedRead = new Regex(@"\b(\w+)\.GetValue(?:OrDefault)?<([\w\.]+)>\(\)");

        /// <summary>
        /// The runtime source, or <c>null</c> when it is not on disk where this test can see it — BH3 is a
        /// submodule and a consumer may have it anywhere, so a missing folder means "cannot check", never
        /// "nothing to check".
        /// </summary>
        private static string RuntimeDirectory()
        {
            var path = Path.Combine(Application.dataPath, "ArcaneOnyx", "BH3", "Runtime");

            return Directory.Exists(path) ? path : null;
        }

        private static IEnumerable<string> RuntimeSources()
        {
            var directory = RuntimeDirectory();

            if (directory == null)
            {
                Assert.Ignore("BH3 runtime sources are not under Assets/ArcaneOnyx/BH3; nothing to scan.");
            }

            return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories);
        }

        private static string Relative(string file) =>
            file.Substring(Application.dataPath.Length).Replace('\\', '/').TrimStart('/');

        /// <summary>C# keywords, which never resolve by name.</summary>
        private static readonly Dictionary<string, System.Type> Aliases = new Dictionary<string, System.Type>
        {
            { "bool", typeof(bool) }, { "byte", typeof(byte) }, { "sbyte", typeof(sbyte) },
            { "char", typeof(char) }, { "decimal", typeof(decimal) }, { "double", typeof(double) },
            { "float", typeof(float) }, { "int", typeof(int) }, { "uint", typeof(uint) },
            { "long", typeof(long) }, { "ulong", typeof(ulong) }, { "short", typeof(short) },
            { "ushort", typeof(ushort) }, { "object", typeof(object) }, { "string", typeof(string) },
        };

        /// <summary>
        /// The type a port declaration or a read names in source, or <c>null</c> when it cannot be pinned
        /// down. The runtime assembly is searched before UnityEngine's so a BH3 type always wins its own
        /// name, and <c>Object</c> — which is ambiguous in any file that uses both namespaces — resolves to
        /// <see cref="UnityEngine.Object"/>, which is what a node file with <c>using UnityEngine</c> means
        /// by it.
        /// </summary>
        private static System.Type Resolve(string name)
        {
            if (Aliases.TryGetValue(name, out var alias)) return alias;

            var shortName = name.Split('.').Last();

            var assemblies = new[]
            {
                typeof(BehaviorTreeNode).Assembly,
                typeof(GameObject).Assembly,
                typeof(Transform).Assembly,
            };

            return assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == shortName);
        }

        /// <summary>
        /// No node reads a port by casting <c>GetValue()</c>.
        ///
        /// <para>
        /// The cast is the bug. <see cref="ValueInput.CanConnectToValid"/> lets the canvas connect any
        /// convertible pair, <c>GetValue()</c> returns the raw boxed object, and unboxing does not convert —
        /// so <c>(float) Speed.GetValue()</c> throws on an int the author was allowed to wire in. There were
        /// forty-odd of these and every one was a latent <c>InvalidCastException</c>; the point of removing
        /// them was never the forty, it was the forty-first.
        /// </para>
        /// </summary>
        [Test]
        public void NoPortIsReadByCastingTheRawValue()
        {
            var offenders = new List<string>();
            int scanned = 0;

            foreach (var file in RuntimeSources())
            {
                scanned++;

                // ValueInput itself is where the raw read legitimately lives -- everything else goes
                // through the typed readers it exposes.
                if (Path.GetFileName(file) == "ValueInput.cs") continue;

                var lines = File.ReadAllLines(file);

                for (int index = 0; index < lines.Length; index++)
                {
                    if (RawCast.IsMatch(lines[index]))
                    {
                        offenders.Add($"{Relative(file)}:{index + 1}  {lines[index].Trim()}");
                    }
                }
            }

            Assert.Greater(scanned, 50, "The scan found almost no sources, so it proved nothing.");

            CollectionAssert.IsEmpty(offenders,
                "Read ports with GetValue<T>() (or GetValueOrDefault<T>() where an unusable value means "
                + "'nothing here'). A cast throws on any connection the editor allowed but whose types only "
                + "convert:\n" + string.Join("\n", offenders));
        }

        /// <summary>
        /// A node reads its port at the type it declared.
        ///
        /// <para>
        /// <c>GenerateRandomNavMeshPosition</c> declared <c>SampleDistance</c> as <c>float</c> and read it
        /// as <c>int</c>; <c>IntegerLiteral</c> and <c>Vector2Literal</c> were the same mistake on the
        /// output side. Under the old raw cast that threw immediately, which at least announced itself.
        /// Under a converting read it does something worse — it silently truncates, and the node quietly
        /// does the wrong thing forever.
        /// </para>
        ///
        /// <para>
        /// Reading a port as a <em>subtype</em> of what it declares is not this mistake: a port typed
        /// <c>Object</c> read as <c>Transform</c> is an ordinary downcast that the node is entitled to make.
        /// Only a read that is neither the declared type nor derived from it is reported.
        /// </para>
        /// </summary>
        [Test]
        public void EveryPortIsReadAtTheTypeItDeclares()
        {
            var mismatches = new List<string>();
            int pairs = 0;

            foreach (var file in RuntimeSources())
            {
                var source = File.ReadAllText(file);

                var declared = Declaration.Matches(source)
                    .Cast<Match>()
                    .GroupBy(match => match.Groups[1].Value)
                    .ToDictionary(group => group.Key, group => group.First().Groups[2].Value);

                if (declared.Count == 0) continue;

                foreach (Match read in TypedRead.Matches(source))
                {
                    var port = read.Groups[1].Value;
                    var readAs = read.Groups[2].Value;

                    // A name that is not a port declared in this same file tells us nothing -- it could be
                    // a local, or a port on another node. Unmatched means unchecked, never assumed fine.
                    if (!declared.TryGetValue(port, out var declaredAs)) continue;

                    pairs++;

                    if (declaredAs == readAs) continue;

                    // Short names, so a declared UnityEngine.Object read as Object is not a false positive.
                    if (declaredAs.Split('.').Last() == readAs.Split('.').Last()) continue;

                    // Narrowing a broadly-typed port is an ordinary downcast the node is entitled to make,
                    // and it is what a port declared Object and read as Transform is doing. Only a read
                    // that is neither the declared type nor derived from it is a mistake. A name that
                    // cannot be resolved to a type is treated as a mismatch rather than waved through --
                    // this gate is worth nothing if it fails open.
                    var declaredType = Resolve(declaredAs);
                    var readType = Resolve(readAs);

                    if (declaredType != null && readType != null
                        && declaredType.IsAssignableFrom(readType)) continue;

                    int line = source.Take(read.Index).Count(c => c == '\n') + 1;
                    mismatches.Add(
                        $"{Relative(file)}:{line}  {port} declares {declaredAs} but is read as {readAs}");
                }
            }

            Assert.Greater(pairs, 40,
                "The scan matched almost no declaration/read pairs, so it proved nothing. If the port "
                + "declaration style changed, this test has to change with it.");

            CollectionAssert.IsEmpty(mismatches,
                "These nodes read a port as a type that is neither what it declares nor derived from it. "
                + "Either the declaration or the read is wrong, and a converting read will hide it rather "
                + "than throw. (Narrowing to a subtype is allowed and is not listed here.)\n"
                + string.Join("\n", mismatches));
        }
    }
}
