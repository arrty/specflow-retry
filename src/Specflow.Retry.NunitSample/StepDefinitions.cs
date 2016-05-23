using System;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Specflow.Retry.NunitSample
{
    [Binding]
    public class StepDefinitions
    {
        private Random _random;
        private double _generatedNumber;

        [Given(@"I have random number generator")]
        public void GivenIHaveRandomNumberGenerator()
        {
            _random = new Random();
        }

        [When(@"it generates number")]
        public void WhenItGeneratesNumber()
        {
            _generatedNumber = _random.NextDouble();
        }

        [Then(@"I'll be lucky if it will be greater then (.*)")]
        public void ThenILlBeLuckyIfItWillBeGreaterThen(double limit)
        {
            Assert.IsTrue(_generatedNumber > limit, "generatedNumber({0}) > limit({1})", _generatedNumber, limit);
        }


        [Given(@"by default scenario context stored value is (.*)")]
        public void GivenByDefaultScenarioContextStoredValueIs(int initialValue)
        {
            if (!ScenarioContext.Current.ContainsKey("SCSV"))
            {
                ScenarioContext.Current["SCSV"] = initialValue;
            }
        }

        [When(@"I increment scenario context stored value")]
        public void WhenIIncrementScenarioContextStoredValue()
        {
            ScenarioContext.Current["SCSV"] = ScenarioContext.Current.Get<int>("SCSV")+1;
        }

        [Then(@"scenario context stored value should be (.*)")]
        public void ThenScenarioContextStoredValueShouldBe(int p0)
        {
            Assert.AreEqual(p0, ScenarioContext.Current.Get<int>("SCSV"));
        }

        [Then(@"scenario should be run (\d*) times")]
        public void ThenScenarioShouldBeRunTimes(int expectedRunTimes)
        {
            int actualRunTimes;
            var key = ScenarioContext.Current.ScenarioInfo.Title + "__RunTimes";
            if (!FeatureContext.Current.TryGetValue(key, out actualRunTimes))
            {
                FeatureContext.Current[key] = actualRunTimes = 1;
            }
            else
            {
                FeatureContext.Current[key] = ++actualRunTimes;
            }

            Assert.AreEqual(expectedRunTimes, actualRunTimes);
        }

        [Then(@"scenario ""(.*)"" should be run (\d*) times")]
        public void ThenScenarioShouldBeRunTimes(string scenarioName, int expectedRunTimes)
        {
            int actualRunTimes;
            var key = ScenarioContext.Current.ScenarioInfo.Title + "__" + scenarioName + "__RunTimes";
            if (!FeatureContext.Current.TryGetValue(key, out actualRunTimes))
            {
                FeatureContext.Current[key] = actualRunTimes = 1;
            }
            else
            {
                FeatureContext.Current[key] = ++actualRunTimes;
            }

            Assert.AreEqual(expectedRunTimes, actualRunTimes);
        }

        [When(@"""(.*)"" thrown")]
        public void WhenSpecFlow_Retry_Sample_CriticalExceptionThrown(string exceptionType)
        {
            var type = Type.GetType(exceptionType);

            throw (Exception)Activator.CreateInstance(type);
        }

        [Then(@"nothing")]
        public void ThenNothing()
        {
            Assert.Fail("Exception wasn't excepted");
        }

    }
}
