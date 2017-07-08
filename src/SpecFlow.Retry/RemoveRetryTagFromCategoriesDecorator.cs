using System.CodeDom;
using TechTalk.SpecFlow.Generator;
using TechTalk.SpecFlow.Generator.UnitTestConverter;

namespace SpecFlow.Retry
{
    public class RemoveRetryTagFromCategoriesDecorator : ITestClassTagDecorator, ITestMethodTagDecorator
    {
        private readonly ITagFilterMatcher _tagFilterMatcher;
        
        public RemoveRetryTagFromCategoriesDecorator(ITagFilterMatcher tagFilterMatcher)
        {
            _tagFilterMatcher = tagFilterMatcher;
        }

        private bool CanDecorateFrom(string tagName)
        {
            var tagNames = new[] {tagName};
            return _tagFilterMatcher.MatchPrefix(TagsRepository.RetryTag, tagNames) ||
                _tagFilterMatcher.MatchPrefix(TagsRepository.RetryExceptTag, tagNames);
        }

        public bool CanDecorateFrom(string tagName, TestClassGenerationContext generationContext)
        {
            return CanDecorateFrom(tagName);
        }

        public void DecorateFrom(string tagName, TestClassGenerationContext generationContext)
        {
        }

        public bool CanDecorateFrom(string tagName, TestClassGenerationContext generationContext, CodeMemberMethod testMethod)
        {
            return CanDecorateFrom(tagName);
        }

        public void DecorateFrom(string tagName, TestClassGenerationContext generationContext, CodeMemberMethod testMethod)
        {            
        }

        int ITestMethodTagDecorator.Priority => PriorityValues.High;

        bool ITestMethodTagDecorator.RemoveProcessedTags => true;

        bool ITestMethodTagDecorator.ApplyOtherDecoratorsForProcessedTags => true;

        int ITestClassTagDecorator.Priority => PriorityValues.High;

        bool ITestClassTagDecorator.RemoveProcessedTags => true;

        bool ITestClassTagDecorator.ApplyOtherDecoratorsForProcessedTags => true;
    }
}
