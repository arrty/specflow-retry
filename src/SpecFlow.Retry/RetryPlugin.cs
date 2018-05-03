using SpecFlow.Retry;
using TechTalk.SpecFlow.Generator.Plugins;
using TechTalk.SpecFlow.Generator.UnitTestConverter;
using TechTalk.SpecFlow.Infrastructure;

[assembly:GeneratorPlugin(typeof(GeneratorPlugin))]
namespace SpecFlow.Retry
{
    public class GeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(GeneratorPluginEvents generatorPluginEvents, GeneratorPluginParameters generatorPluginParameters)
        {
            generatorPluginEvents.RegisterDependencies += (sender, args) =>
                {
                    args.ObjectContainer.RegisterTypeAs<RetryUnitTestFeatureGenerator, IFeatureGenerator>();
                    args.ObjectContainer.RegisterTypeAs<RetryUnitTestFeatureGeneratorProvider, IFeatureGeneratorProvider>("retry");
                    args.ObjectContainer.RegisterTypeAs<RemoveRetryTagFromCategoriesDecorator, ITestClassTagDecorator>("retry");
                    args.ObjectContainer.RegisterTypeAs<RemoveRetryTagFromCategoriesDecorator, ITestMethodTagDecorator>("retry");
                };
        }
    }
}

