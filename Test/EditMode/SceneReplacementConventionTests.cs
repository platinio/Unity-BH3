using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// One convention about replacing the open scene, checked by reading the editor source.
    ///
    /// <para>
    /// <c>EditorSceneManager.NewScene(..., NewSceneMode.Single)</c> closes every open scene and discards
    /// unsaved changes without prompting and without erroring, so a tool that calls it and does not ask
    /// first can destroy whatever the user had open. That is not something a behaviour test can catch: the
    /// only way to observe the loss is to suffer it, and a test that entered the failure would take the
    /// developer's own scene with it.
    /// </para>
    ///
    /// <para>
    /// Source scanning follows <see cref="PortReadConventionTests"/>, and carries the same caveats. It is
    /// file-granular: it proves the question is asked somewhere in a file that replaces the scene, not that
    /// it is asked before the call or that the answer is obeyed. It asserts it found a call to scan, so it
    /// cannot pass by quietly checking nothing.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SceneReplacementConventionTests
    {
        private const string ReplacesTheOpenScene = "EditorSceneManager.NewScene(";
        private const string AsksFirst = "SaveCurrentModifiedScenesIfUserWantsTo(";

        /// <summary>
        /// The editor source, or <c>null</c> when it is not on disk where this test can see it -- BH3 is a
        /// submodule and a consumer may have it anywhere, so a missing folder means "cannot check", never
        /// "nothing to check".
        /// </summary>
        private static IEnumerable<string> EditorSources()
        {
            var path = Path.Combine(Application.dataPath, "ArcaneOnyx", "BH3", "Editor");

            if (!Directory.Exists(path))
            {
                Assert.Ignore("BH3 editor sources are not under Assets/ArcaneOnyx/BH3; nothing to scan.");
            }

            return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories);
        }

        private static string Relative(string file) =>
            file.Substring(Application.dataPath.Length).Replace('\\', '/').TrimStart('/');

        [Test]
        public void NothingReplacesTheOpenSceneWithoutOfferingToSaveIt()
        {
            var replacers = new List<string>();
            var offenders = new List<string>();

            foreach (var file in EditorSources())
            {
                var source = File.ReadAllText(file);

                if (!source.Contains(ReplacesTheOpenScene)) continue;

                replacers.Add(Relative(file));

                if (!source.Contains(AsksFirst)) offenders.Add(Relative(file));
            }

            Assert.IsNotEmpty(replacers,
                "Found nothing that replaces the open scene, so this test checked nothing. Either the "
                + "convention no longer has a subject or the scan stopped matching.");

            CollectionAssert.IsEmpty(offenders,
                "These replace every open scene and discard unsaved changes without asking. Call "
                + "EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() first and abort when it "
                + "returns false:" + System.Environment.NewLine
                + string.Join(System.Environment.NewLine, offenders));
        }
    }
}
