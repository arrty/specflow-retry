namespace SpecFlow.RetryCore
{
    using System.CodeDom;
    using TechTalk.SpecFlow.Generator;
    using TechTalk.SpecFlow.Generator.UnitTestConverter;

    public class RemoveRetryTagFromCategoriesDecorator : ITestClassTagDecorator, ITestMethodTagDecorator
    {
        private readonly ITagFilterMatcher _tagFilterMatcher;

        public RemoveRetryTagFromCategoriesDecorator(ITagFilterMatcher tagFilterMatcher)
        {
            this._tagFilterMatcher = tagFilterMatcher;
        }

        private bool CanDecorateFrom(string tagName)
        {
            var tagNames = new[] { tagName };
            return this._tagFilterMatcher.MatchPrefix(TagsRepository.RetryTag, tagNames) ||
                this._tagFilterMatcher.MatchPrefix(TagsRepository.RetryExceptTag, tagNames);
        }

        public bool CanDecorateFrom(string tagName, TestClassGenerationContext generationContext)
        {
            return this.CanDecorateFrom(tagName);
        }

        public bool CanDecorateFrom(string tagName, TestClassGenerationContext generationContext, CodeMemberMethod testMethod)
        {
            return this.CanDecorateFrom(tagName);
        }

        public void DecorateFrom(string tagName, TestClassGenerationContext generationContext)
        {
            // Method intentionally left empty.
        }

        public void DecorateFrom(string tagName, TestClassGenerationContext generationContext, CodeMemberMethod testMethod)
        {
            // Method intentionally left empty.
        }

        int ITestMethodTagDecorator.Priority => PriorityValues.High;

        bool ITestMethodTagDecorator.RemoveProcessedTags => true;

        bool ITestMethodTagDecorator.ApplyOtherDecoratorsForProcessedTags => true;

        int ITestClassTagDecorator.Priority => PriorityValues.High;

        bool ITestClassTagDecorator.RemoveProcessedTags => true;

        bool ITestClassTagDecorator.ApplyOtherDecoratorsForProcessedTags => true;
    }
}
