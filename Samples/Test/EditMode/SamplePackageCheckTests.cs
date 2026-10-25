using System.Linq;
using NUnit.Framework;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class SamplePackageCheckTests
    {
        [Test]
        public void NamesEachRequiredPackageThatIsNotInstalled()
        {
            var missing = SamplePackageCheck.MissingPackages(new[] { "com.unity.inputsystem", "com.unity.ugui" });

            Assert.That(missing, Is.EqualTo(new[] { "com.unity.ai.navigation" }));
        }

        [Test]
        public void NamesNothingWhenEveryRequiredPackageIsInstalled()
        {
            var installed = SamplePackageCheck.RequiredPackages.Append("com.unity.ugui");

            Assert.That(SamplePackageCheck.MissingPackages(installed), Is.Empty);
        }
    }
}
