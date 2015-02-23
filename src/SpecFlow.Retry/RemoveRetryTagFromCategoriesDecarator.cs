using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechTalk.SpecFlow.Generator;
using TechTalk.SpecFlow.Generator.UnitTestConverter;

namespace SpecFlow.Retry
{
    public class RemoveRetryTagFromCategoriesDecarator : ITestClassTagDecorator, ITestMethodTagDecorator
    {
        private readonly ITagFilterMatcher tagFilterMatcher;

        public RemoveRetryTagFromCategoriesDecarator(ITagFilterMatcher tagFilterMatcher)
        {
            this.tagFilterMatcher = tagFilterMatcher;
        }

        private bool CanDecorateFrom(string tagName)
        {
            var tagNames = new string[] {tagName};
            return tagFilterMatcher.MatchPrefix(TagsRepository.RETRY_TAG, tagNames)
                   || tagFilterMatcher.MatchPrefix(TagsRepository.RETRY_EXCEPT_TAG, tagNames);
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

        int ITestMethodTagDecorator.Priority
        {
            get { return PriorityValues.High; }
        }

        bool ITestMethodTagDecorator.RemoveProcessedTags
        {
            get { return true; }
        }

        bool ITestMethodTagDecorator.ApplyOtherDecoratorsForProcessedTags
        {
            get { return true; }
        }

        int ITestClassTagDecorator.Priority
        {
            get { return PriorityValues.High; }
        }

        bool ITestClassTagDecorator.RemoveProcessedTags
        {
            get { return true; }
        }

        bool ITestClassTagDecorator.ApplyOtherDecoratorsForProcessedTags
        {
            get { return true; }
        }
    }
}
