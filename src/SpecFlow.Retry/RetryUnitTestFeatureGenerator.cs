using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gherkin.Ast;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Generator;
using TechTalk.SpecFlow.Generator.Configuration;
using TechTalk.SpecFlow.Generator.UnitTestConverter;
using TechTalk.SpecFlow.Generator.UnitTestProvider;
using TechTalk.SpecFlow.Parser;
using TechTalk.SpecFlow.Tracing;
using TechTalk.SpecFlow.Utils;

namespace SpecFlow.Retry
{
    public class RetryUnitTestFeatureGenerator : IFeatureGenerator
    {
        private const string DEFAULT_NAMESPACE = "SpecFlowTests";
        const string TESTCLASS_NAME_FORMAT = "{0}Feature";
        const string TEST_NAME_FORMAT = "{0}";
        private const string IGNORE_TAG = "@Ignore";
        private const string SCENARIO_INITIALIZE_NAME = "ScenarioSetup";
        private const string SCENARIO_CLEANUP_NAME = "ScenarioCleanup";
        private const string TEST_INITIALIZE_NAME = "TestInitialize";
        private const string TEST_CLEANUP_NAME = "ScenarioTearDown";
        private const string TESTCLASS_INITIALIZE_NAME = "FeatureSetup";
        private const string TESTCLASS_CLEANUP_NAME = "FeatureTearDown";
        private const string BACKGROUND_NAME = "FeatureBackground";
        private const string TESTRUNNER_FIELD = "testRunner";
        private const string SPECFLOW_NAMESPACE = "TechTalk.SpecFlow";
        private const string SCENARIO_OUTLINE_EXAMPLE_TAGS_PARAMETER = "exampleTags";

        private readonly IUnitTestGeneratorProvider testGeneratorProvider;
        private readonly CodeDomHelper codeDomHelper;
        private readonly GeneratorConfiguration generatorConfiguration;
        private readonly IDecoratorRegistry decoratorRegistry;
        private readonly ITagFilterMatcher tagFilterMatcher;

        public RetryUnitTestFeatureGenerator(IUnitTestGeneratorProvider testGeneratorProvider, CodeDomHelper codeDomHelper,
            GeneratorConfiguration generatorConfiguration, IDecoratorRegistry decoratorRegistry, ITagFilterMatcher tagFilterMatcher)
        {
            this.testGeneratorProvider = testGeneratorProvider;
            this.codeDomHelper = codeDomHelper;
            this.generatorConfiguration = generatorConfiguration;
            this.decoratorRegistry = decoratorRegistry;
            this.tagFilterMatcher = tagFilterMatcher;
        }

        private CodeMemberMethod CreateMethod(CodeTypeDeclaration type)
        {
            CodeMemberMethod method = new CodeMemberMethod();
            type.Members.Add(method);
            return method;
        }

        private static bool HasFeatureBackground(SpecFlowFeature feature)
        {
            return feature.Background != null;
        }

        private TestClassGenerationContext CreateTestClassStructure(CodeNamespace codeNamespace, string testClassName, SpecFlowFeature feature)
        {
            var testClass = codeDomHelper.CreateGeneratedTypeDeclaration(testClassName);
            codeNamespace.Types.Add(testClass);

            return new TestClassGenerationContext(
                testGeneratorProvider,
                feature,
                codeNamespace, 
                testClass,
                DeclareTestRunnerMember(testClass),
                CreateMethod(testClass),
                CreateMethod(testClass),
                CreateMethod(testClass),
                CreateMethod(testClass),
                CreateMethod(testClass),
                CreateMethod(testClass),
                HasFeatureBackground(feature) ? CreateMethod(testClass) : null,
                generateRowTests: testGeneratorProvider.GetTraits().HasFlag(UnitTestGeneratorTraits.RowTests) && generatorConfiguration.AllowRowTests);
        }

        private CodeNamespace CreateNamespace(string targetNamespace)
        {
            targetNamespace = targetNamespace ?? DEFAULT_NAMESPACE;

            CodeNamespace codeNamespace = new CodeNamespace(targetNamespace);

            codeNamespace.Imports.Add(new CodeNamespaceImport(SPECFLOW_NAMESPACE));
            return codeNamespace;
        }

        public CodeNamespace GenerateUnitTestFixture(SpecFlowFeature feature, string testClassName, string targetNamespace)
        {
            CodeNamespace codeNamespace = CreateNamespace(targetNamespace);

            testClassName = testClassName ?? string.Format(TESTCLASS_NAME_FORMAT, feature.Name.ToIdentifier());
            var generationContext = CreateTestClassStructure(codeNamespace, testClassName, feature);

            SetupTestClass(generationContext);
            SetupTestClassInitializeMethod(generationContext);
            SetupTestClassCleanupMethod(generationContext);

            SetupScenarioInitializeMethod(generationContext);
            SetupFeatureBackground(generationContext);
            SetupScenarioCleanupMethod(generationContext);

            SetupTestInitializeMethod(generationContext);
            SetupTestCleanupMethod(generationContext);


            foreach (var scenarioDefinition in feature.ScenarioDefinitions)
            {
                if (string.IsNullOrEmpty(scenarioDefinition.Name))
                    throw new TestGeneratorException("The scenario must have a title specified.");

                var scenarioOutline = scenarioDefinition as ScenarioOutline;
                if (scenarioOutline != null)
                    GenerateScenarioOutlineTest(generationContext, scenarioOutline);
                else
                    GenerateTest(generationContext, (Scenario)scenarioDefinition);
            }
            
            //before return the generated code, call generate provider's method in case the provider want to customerize the generated code            
            testGeneratorProvider.FinalizeTestClass(generationContext);
            return codeNamespace;
        }

        private void SetupScenarioCleanupMethod(TestClassGenerationContext generationContext)
        {
            CodeMemberMethod scenarioCleanupMethod = generationContext.ScenarioCleanupMethod;

            scenarioCleanupMethod.Attributes = MemberAttributes.Public;
            scenarioCleanupMethod.Name = SCENARIO_CLEANUP_NAME;

            // call collect errors
            var testRunnerField = GetTestRunnerExpression();
            //testRunner.CollectScenarioErrors();
            scenarioCleanupMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    testRunnerField,
                    "CollectScenarioErrors"));
        }

        private void SetupTestClass(TestClassGenerationContext generationContext)
        {
            generationContext.TestClass.IsPartial = true;
            generationContext.TestClass.TypeAttributes |= TypeAttributes.Public;

            AddLinePragmaInitial(generationContext.TestClass, generationContext.Feature.SourceFilePath);

            testGeneratorProvider.SetTestClass(generationContext, generationContext.Feature.Name, generationContext.Feature.Description);

            List<string> featureCategories;
            decoratorRegistry.DecorateTestClass(generationContext, out featureCategories);

            if (featureCategories.Any())
                testGeneratorProvider.SetTestClassCategories(generationContext, featureCategories);
        }

        private CodeMemberField DeclareTestRunnerMember(CodeTypeDeclaration type)
        {
            CodeMemberField testRunnerField = new CodeMemberField(typeof(ITestRunner), TESTRUNNER_FIELD);
            type.Members.Add(testRunnerField);
            return testRunnerField;
        }

        private CodeExpression GetTestRunnerExpression()
        {
            return new CodeVariableReferenceExpression(TESTRUNNER_FIELD);
        }

        private IEnumerable<string> GetNonIgnoreTags(IEnumerable<Tag> tags)
        {
            return tags.Where(t => !t.Name.Equals(IGNORE_TAG, StringComparison.InvariantCultureIgnoreCase)).Select(t => t.GetNameWithoutAt());
        }

        private bool HasIgnoreTag(IEnumerable<Tag> tags)
        {
            return tags.Any(t => t.Name.Equals(IGNORE_TAG, StringComparison.InvariantCultureIgnoreCase));
        }

        private void SetupTestClassInitializeMethod(TestClassGenerationContext generationContext)
        {
            var testClassInitializeMethod = generationContext.TestClassInitializeMethod;

            testClassInitializeMethod.Attributes = MemberAttributes.Public;
            testClassInitializeMethod.Name = TESTCLASS_INITIALIZE_NAME;

            testGeneratorProvider.SetTestClassInitializeMethod(generationContext);

            //testRunner = TestRunnerManager.GetTestRunner(); if UnitTestGeneratorTraits.ParallelExecution
            //testRunner = TestRunnerManager.GetTestRunner(null, 0); if not UnitTestGeneratorTraits.ParallelExecution
            var testRunnerField = GetTestRunnerExpression();

            var testRunnerParameters = testGeneratorProvider.GetTraits().HasFlag(UnitTestGeneratorTraits.ParallelExecution) ?
                new CodeExpression[]{} : new []  {new CodePrimitiveExpression(null), new CodePrimitiveExpression(0) };

            testClassInitializeMethod.Statements.Add(
                new CodeAssignStatement(
                    testRunnerField,
                    new CodeMethodInvokeExpression(
                        new CodeTypeReferenceExpression(typeof(TestRunnerManager)),
                        "GetTestRunner", testRunnerParameters)));

            //FeatureInfo featureInfo = new FeatureInfo("xxxx");
            testClassInitializeMethod.Statements.Add(
                new CodeVariableDeclarationStatement(typeof(FeatureInfo), "featureInfo",
                    new CodeObjectCreateExpression(typeof(FeatureInfo),
                        new CodeObjectCreateExpression(typeof(CultureInfo),
                            new CodePrimitiveExpression(generationContext.Feature.Language)),
                        new CodePrimitiveExpression(generationContext.Feature.Name),
                        new CodePrimitiveExpression(generationContext.Feature.Description),
                        new CodeFieldReferenceExpression(
                            new CodeTypeReferenceExpression("ProgrammingLanguage"),
                            codeDomHelper.TargetLanguage.ToString()),
                        GetStringArrayExpression(generationContext.Feature.Tags))));

            //testRunner.OnFeatureStart(featureInfo);
            testClassInitializeMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    testRunnerField,
                    "OnFeatureStart",
                    new CodeVariableReferenceExpression("featureInfo")));
        }

        private CodeExpression GetStringArrayExpression(IEnumerable<Tag> tags)
        {
            if (!tags.Any())
                return new CodeCastExpression(typeof(string[]), new CodePrimitiveExpression(null));

            return new CodeArrayCreateExpression(typeof(string[]), tags.Select(tag => new CodePrimitiveExpression(tag.GetNameWithoutAt())).Cast<CodeExpression>().ToArray());
        }

        private CodeExpression GetStringArrayExpression(IEnumerable<string> items, ParameterSubstitution paramToIdentifier)
        {
            return new CodeArrayCreateExpression(typeof(string[]), items.Select(item => GetSubstitutedString(item, paramToIdentifier)).ToArray());
        }

        private void SetupTestClassCleanupMethod(TestClassGenerationContext generationContext)
        {
            CodeMemberMethod testClassCleanupMethod = generationContext.TestClassCleanupMethod;

            testClassCleanupMethod.Attributes = MemberAttributes.Public;
            testClassCleanupMethod.Name = TESTCLASS_CLEANUP_NAME;

            testGeneratorProvider.SetTestClassCleanupMethod(generationContext);

            var testRunnerField = GetTestRunnerExpression();
            //            testRunner.OnFeatureEnd();
            testClassCleanupMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    testRunnerField,
                    "OnFeatureEnd"));
            //            testRunner = null;
            testClassCleanupMethod.Statements.Add(
                new CodeAssignStatement(
                    testRunnerField,
                    new CodePrimitiveExpression(null)));
        }

        private void SetupTestInitializeMethod(TestClassGenerationContext generationContext)
        {
            CodeMemberMethod testInitializeMethod = generationContext.TestInitializeMethod;

            testInitializeMethod.Attributes = MemberAttributes.Public;
            testInitializeMethod.Name = TEST_INITIALIZE_NAME;

            testGeneratorProvider.SetTestInitializeMethod(generationContext);
        }

        private void SetupTestCleanupMethod(TestClassGenerationContext generationContext)
        {
            CodeMemberMethod testCleanupMethod = generationContext.TestCleanupMethod;

            testCleanupMethod.Attributes = MemberAttributes.Public;
            testCleanupMethod.Name = TEST_CLEANUP_NAME;

            testGeneratorProvider.SetTestCleanupMethod(generationContext);

            var testRunnerField = GetTestRunnerExpression();
            //testRunner.OnScenarioEnd();
            testCleanupMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    testRunnerField,
                    "OnScenarioEnd"));
        }

        private void SetupScenarioInitializeMethod(TestClassGenerationContext generationContext)
        {
            CodeMemberMethod scenarioInitializeMethod = generationContext.ScenarioInitializeMethod;

            scenarioInitializeMethod.Attributes = MemberAttributes.Public;
            scenarioInitializeMethod.Name = SCENARIO_INITIALIZE_NAME;
            scenarioInitializeMethod.Parameters.Add(
                new CodeParameterDeclarationExpression(typeof(ScenarioInfo), "scenarioInfo"));

            //testRunner.OnScenarioStart(scenarioInfo);
            var testRunnerField = GetTestRunnerExpression();
            scenarioInitializeMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    testRunnerField,
                    "OnScenarioStart",
                    new CodeVariableReferenceExpression("scenarioInfo")));
        }

        private void SetupFeatureBackground(TestClassGenerationContext generationContext)
        {
            if (!HasFeatureBackground(generationContext.Feature))
                return;

            var background = generationContext.Feature.Background;

            CodeMemberMethod backgroundMethod = generationContext.FeatureBackgroundMethod;

            backgroundMethod.Attributes = MemberAttributes.Public;
            backgroundMethod.Name = BACKGROUND_NAME;

            AddLineDirective(backgroundMethod.Statements, background);

            foreach (var step in background.Steps)
                GenerateStep(backgroundMethod, step, null);

			AddLineDirectiveHidden(backgroundMethod.Statements);
        }

        private class ParameterSubstitution : List<KeyValuePair<string, string>>
        {
            public void Add(string parameter, string identifier)
            {
                Add(new KeyValuePair<string, string>(parameter.Trim(), identifier));
            }

            public bool TryGetIdentifier(string param, out string id)
            {
                param = param.Trim();
                foreach (var pair in this)
                {
                    if (pair.Key.Equals(param))
                    {
                        id = pair.Value;
                        return true;
                    }
                }
                id = null;
                return false;
            }
        }

        private void GenerateScenarioOutlineTest(TestClassGenerationContext generationContext, ScenarioOutline scenarioOutline)
        {
            ValidateExampleSetConsistency(scenarioOutline);

            ParameterSubstitution paramToIdentifier = CreateParamToIdentifierMapping(scenarioOutline);

            var scenatioOutlineTestMethod = CreateScenarioOutlineTestMethod(generationContext, scenarioOutline, paramToIdentifier);
            var exampleTagsParam = new CodeVariableReferenceExpression(SCENARIO_OUTLINE_EXAMPLE_TAGS_PARAMETER);

            if (generationContext.GenerateRowTests)
            {
                GenerateScenarioOutlineExamplesAsRowTests(generationContext, scenarioOutline, scenatioOutlineTestMethod);
            }
            else
            {
                GenerateScenarioOutlineExamplesAsIndividualMethods(scenarioOutline, generationContext, scenatioOutlineTestMethod, paramToIdentifier);
            }
            int retryValue;
            if (GetTagValue(generationContext, scenarioOutline, TagsRepository.RetryTag, Int32.TryParse, out retryValue))
            {
                string retryExceptExceptionName;

                GetTagValue(generationContext, scenarioOutline, TagsRepository.RetryExceptTag, out retryExceptExceptionName);

                scenatioOutlineTestMethod = GenerateRetryStatementAndUnwrap(generationContext, scenatioOutlineTestMethod, retryValue, retryExceptExceptionName);
            }

            GenerateTestBody(generationContext, scenarioOutline, scenatioOutlineTestMethod, exampleTagsParam, paramToIdentifier);
        }

        private void GenerateScenarioOutlineExamplesAsIndividualMethods(ScenarioOutline scenarioOutline, TestClassGenerationContext generationContext, CodeMemberMethod scenatioOutlineTestMethod, ParameterSubstitution paramToIdentifier)
        {
            int exampleSetIndex = 0;
            foreach (var exampleSet in scenarioOutline.Examples)
            {
                bool useFirstColumnAsName = CanUseFirstColumnAsName(exampleSet.TableBody);
                string exampleSetIdentifier = string.IsNullOrEmpty(exampleSet.Name)
                                                  ? scenarioOutline.Examples.Count(es => string.IsNullOrEmpty(es.Name)) > 1
                                                        ? string.Format("ExampleSet {0}", exampleSetIndex).ToIdentifier()
                                                        : null
                                                  : exampleSet.Name.ToIdentifier();

                foreach (var example in exampleSet.TableBody.Select((r, i) => new { Row = r, Index = i}))
                {
                    string variantName = useFirstColumnAsName ? example.Row.Cells.First().Value : string.Format("Variant {0}", example.Index);
                    GenerateScenarioOutlineTestVariant(generationContext, scenarioOutline, scenatioOutlineTestMethod, paramToIdentifier, exampleSet.Name ?? "", exampleSetIdentifier, example.Row, exampleSet.Tags, variantName);
                }
                exampleSetIndex++;
            }
        }

        private void GenerateScenarioOutlineExamplesAsRowTests(TestClassGenerationContext generationContext, ScenarioOutline scenarioOutline, CodeMemberMethod scenatioOutlineTestMethod)
        {
            SetupTestMethod(generationContext, scenatioOutlineTestMethod, scenarioOutline, null, null, null, rowTest: true);

            foreach (var examples in scenarioOutline.Examples)
            {
                foreach (var row in examples.TableBody)
                {
                    var arguments = row.Cells.Select(c => c.Value);
                    testGeneratorProvider.SetRow(generationContext, scenatioOutlineTestMethod, arguments, GetNonIgnoreTags(examples.Tags), HasIgnoreTag(examples.Tags));
                }
            }
        }

        private ParameterSubstitution CreateParamToIdentifierMapping(ScenarioOutline scenarioOutline)
        {
            ParameterSubstitution paramToIdentifier = new ParameterSubstitution();
            foreach (var param in scenarioOutline.Examples.First().TableHeader.Cells)
                paramToIdentifier.Add(param.Value, param.Value.ToIdentifierCamelCase());
            return paramToIdentifier;
        }

        private void ValidateExampleSetConsistency(ScenarioOutline scenarioOutline)
        {
            if (scenarioOutline.Examples.Count() <= 1)
                return;

            var firstExamplesHeader =
                scenarioOutline.Examples.First().TableHeader.Cells.Select(c => c.Value).ToArray();

            //check params
            if (scenarioOutline.Examples.Skip(1)
                .Select(examples => examples.TableHeader.Cells.Select(c => c.Value))
                .Any(paramNames => !paramNames.SequenceEqual(firstExamplesHeader)))
            {
                throw new TestGeneratorException("The example sets must provide the same parameters.");
            }
        }

        private bool CanUseFirstColumnAsName(IEnumerable<Gherkin.Ast.TableRow> tableBody)
        {
            if (tableBody.Any(r => !r.Cells.Any()))
                return false;

            return tableBody.Select(r => r.Cells.First().Value.ToIdentifier()).Distinct().Count() == tableBody.Count();
        }

        private CodeMemberMethod CreateScenarioOutlineTestMethod(TestClassGenerationContext generationContext, ScenarioOutline scenarioOutline, ParameterSubstitution paramToIdentifier)
        {
            CodeMemberMethod testMethod = CreateMethod(generationContext.TestClass);

            testMethod.Attributes = MemberAttributes.Public;
            testMethod.Name = string.Format(TEST_NAME_FORMAT, scenarioOutline.Name.ToIdentifier());

            foreach (var pair in paramToIdentifier)
            {
                testMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), pair.Value));
            }

            testMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof (string[]), SCENARIO_OUTLINE_EXAMPLE_TAGS_PARAMETER));
            return testMethod;
        }

        private void GenerateScenarioOutlineTestVariant(TestClassGenerationContext generationContext, ScenarioOutline scenarioOutline, CodeMemberMethod scenatioOutlineTestMethod, 
            IEnumerable<KeyValuePair<string, string>> paramToIdentifier, string exampleSetTitle, string exampleSetIdentifier,
            Gherkin.Ast.TableRow row, IEnumerable<Tag> exampleSetTags, string variantName)
        {
            
            CodeMemberMethod testMethod = CreateTestMethod(generationContext, scenarioOutline, exampleSetTags, variantName, exampleSetIdentifier);
            //call test implementation with the params
            List<CodeExpression> argumentExpressions = row.Cells.Select(paramCell => new CodePrimitiveExpression(paramCell.Value)).Cast<CodeExpression>().ToList();

            argumentExpressions.Add(GetStringArrayExpression(exampleSetTags));

            testMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    new CodeThisReferenceExpression(),
                    scenatioOutlineTestMethod.Name,
                    argumentExpressions.ToArray()));

            AddLineDirectiveHidden(testMethod.Statements);
            var arguments = paramToIdentifier.Select((p2i, paramIndex) => new KeyValuePair<string, string>(p2i.Key, row.Cells.ElementAt(paramIndex).Value)).ToList();
            testGeneratorProvider.SetTestMethodAsRow(generationContext, testMethod, scenarioOutline.Name, exampleSetTitle, variantName, arguments);
        }

        private CodeMemberMethod CreateTestMethod(TestClassGenerationContext generationContext, ScenarioDefinition scenario, IEnumerable<Tag> additionalTags, string variantName = null, string exampleSetIdentifier = null)
        {
            CodeMemberMethod testMethod = CreateMethod(generationContext.TestClass);

            SetupTestMethod(generationContext, testMethod, scenario, additionalTags,variantName,exampleSetIdentifier);

            return testMethod;
        }

        private void GenerateTest(TestClassGenerationContext generationContext, Scenario scenario)
        {
            CodeMemberMethod testMethod = CreateTestMethod(generationContext, scenario, null);

            int retryValue;
            if (GetTagValue(generationContext, scenario, TagsRepository.RetryTag, Int32.TryParse, out retryValue))
            {
                string retryExceptExceptionName;

                GetTagValue(generationContext, scenario, TagsRepository.RetryExceptTag, out retryExceptExceptionName);

                testMethod = GenerateRetryStatementAndUnwrap(generationContext, testMethod, retryValue, retryExceptExceptionName);
            }

            GenerateTestBody(generationContext, scenario, testMethod);
        }

        private delegate bool TryParseDelegate<T>(string value, out T parsedValue);

        private bool GetTagValue(TestClassGenerationContext generationContext, ScenarioOutline scenarioOutline, string retryTag, out string value)
        {
            return GetTagValue(generationContext, scenarioOutline, retryTag, delegate (string v, out string p) { p = v; return v != null; }, out value);
        }

        private bool GetTagValue(TestClassGenerationContext generationContext, Scenario scenario, string retryTag, out string value)
        {
            return GetTagValue(generationContext, scenario, retryTag, delegate (string v, out string p) { p = v; return v != null; }, out value);
        }

        private bool GetTagValue<T>(TestClassGenerationContext generationContext, ScenarioOutline scenarioOutline, string retryTag, TryParseDelegate<T> parser, out T value)
        {
            value = default(T);
            var tagNames = scenarioOutline.Tags?.Select(_ => _.GetNameWithoutAt()) ?? new string[0];
            tagNames = tagNames.ToList();
            string retryCountValue;
            return tagFilterMatcher.GetTagValue(retryTag, tagNames, out retryCountValue)
                       && parser(retryCountValue, out value) ||
                   tagFilterMatcher.GetTagValue(retryTag, generationContext.Feature, out retryCountValue)
                       && parser(retryCountValue, out value);
        }

        private bool GetTagValue<T>(TestClassGenerationContext generationContext, Scenario scenario, string retryTag, TryParseDelegate<T> parser, out T value)
        {
            value = default(T);
            var tagNames = scenario.Tags?.Select(_ => _.GetNameWithoutAt()) ?? new string[0];
            tagNames = tagNames.ToList();
            string retryCountValue;
            return tagFilterMatcher.GetTagValue(retryTag, tagNames, out retryCountValue)
                       && parser(retryCountValue, out value) ||
                   tagFilterMatcher.GetTagValue(retryTag, generationContext.Feature, out retryCountValue)
                       && parser(retryCountValue, out value);
        }

        private CodeMemberMethod GenerateRetryStatementAndUnwrap(TestClassGenerationContext generationContext, CodeMemberMethod testMethod, int retryCount, string retryExceptExceptionName)
        {
            var method = CreateMethod(generationContext.TestClass);

            method.Name = testMethod.Name + "Internal";

            foreach (CodeParameterDeclarationExpression parameter in testMethod.Parameters)
            {
                method.Parameters.Add(new CodeParameterDeclarationExpression(parameter.Type, parameter.Name));
            }

            testMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(Exception), "lastException", new CodePrimitiveExpression(null)));

            var codeCatchClauses = new List<CodeCatchClause>();

            if (!String.IsNullOrEmpty(retryExceptExceptionName))
            {
                codeCatchClauses.Add(new CodeCatchClause(
                    "exc",
                    new CodeTypeReference(new CodeTypeParameter(retryExceptExceptionName)),
                        new CodeThrowExceptionStatement()
                    ));
            }

            codeCatchClauses.Add(new CodeCatchClause(
                "exc",
                new CodeTypeReference(typeof(Exception)),
                /* lastException = exc; */
                new CodeAssignStatement(
                    new CodeVariableReferenceExpression("lastException"),
                    new CodeVariableReferenceExpression("exc"))));

            testMethod.Statements.Add(
                new CodeIterationStatement(
                    /* for(var i = 0; i < retryCount; i++)*/
                    initStatement:
                        new CodeVariableDeclarationStatement(typeof(int), "i", new CodePrimitiveExpression(0)),
                    testExpression: new CodeBinaryOperatorExpression(
                        new CodeVariableReferenceExpression("i"),
                        CodeBinaryOperatorType.LessThanOrEqual,
                        new CodePrimitiveExpression(retryCount)
                        ),
                    incrementStatement: new CodeAssignStatement(
                        new CodeVariableReferenceExpression("i"),
                        new CodeBinaryOperatorExpression(
                            new CodeVariableReferenceExpression("i"),
                            CodeBinaryOperatorType.Add,
                            new CodePrimitiveExpression(1))),
                    statements: new CodeStatement[]
                    {
                        /* try */
                        new CodeTryCatchFinallyStatement(
                            tryStatements: new CodeStatement[]
                            {
                                /* { */
                                /*  this.TestInternal(); */
                                new CodeExpressionStatement(new CodeMethodInvokeExpression(
                                    new CodeThisReferenceExpression(), method.Name,
                                        method.Parameters
                                        .Cast<CodeParameterDeclarationExpression>()
                                        .Select(_=>new CodeVariableReferenceExpression(_.Name))
                                        .Cast<CodeExpression>()
                                        .ToArray())),
                                /*  return; */
                                new CodeMethodReturnStatement(),
                                /* } */
                            },
                            catchClauses: codeCatchClauses.ToArray()
                            ),
                        new CodeConditionStatement(
                            /* if ( (i+1) >= retryCount) */
                            new CodeBinaryOperatorExpression(
                                new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("i"), CodeBinaryOperatorType.Add, new CodePrimitiveExpression(1)),
                                CodeBinaryOperatorType.LessThanOrEqual,
                                new CodePrimitiveExpression(retryCount)
                                ),
                            /* { */
                            /*  testRunner.OnScenarioEnd();*/
                            new CodeExpressionStatement(
                                new CodeMethodInvokeExpression(
                                    GetTestRunnerExpression(),
                                    "OnScenarioEnd"
                                    )))
                            /* } */
                    })
                );
            testMethod.Statements.Add(new CodeConditionStatement(
                condition: new CodeBinaryOperatorExpression(
                    new CodeVariableReferenceExpression("lastException"),
                    CodeBinaryOperatorType.IdentityInequality,
                    new CodePrimitiveExpression(null)),
                trueStatements: new CodeThrowExceptionStatement(new CodeVariableReferenceExpression("lastException"))));

            return method;
        }

        private void GenerateTestBody(TestClassGenerationContext generationContext, ScenarioDefinition scenario, CodeMemberMethod testMethod, CodeExpression additionalTagsExpression = null, ParameterSubstitution paramToIdentifier = null)
        {
            //call test setup
            //ScenarioInfo scenarioInfo = new ScenarioInfo("xxxx", tags...);
            CodeExpression tagsExpression;
            if (additionalTagsExpression == null)
                tagsExpression = GetStringArrayExpression(scenario.Tags);
            else if (!scenario.HasTags())
                tagsExpression = additionalTagsExpression;
            else
            {
                // merge tags list
                // var tags = tags1
                // if (tags2 != null)
                //   tags = Enumerable.ToArray(Enumerable.Concat(tags1, tags1));
                testMethod.Statements.Add(
                    new CodeVariableDeclarationStatement(typeof(string[]), "__tags", GetStringArrayExpression(scenario.Tags)));
                tagsExpression = new CodeVariableReferenceExpression("__tags");
                testMethod.Statements.Add(
                    new CodeConditionStatement(
                        new CodeBinaryOperatorExpression(
                            additionalTagsExpression, 
                            CodeBinaryOperatorType.IdentityInequality, 
                            new CodePrimitiveExpression(null)),
                        new CodeAssignStatement(
                            tagsExpression, 
                            new CodeMethodInvokeExpression(
                                new CodeTypeReferenceExpression(typeof (Enumerable)),
                                "ToArray",
                                new CodeMethodInvokeExpression(
                                    new CodeTypeReferenceExpression(typeof (Enumerable)),
                                    "Concat",
                                    tagsExpression,
                                    additionalTagsExpression)))));
            }
            testMethod.Statements.Add(
                new CodeVariableDeclarationStatement(typeof(ScenarioInfo), "scenarioInfo",
                    new CodeObjectCreateExpression(typeof(ScenarioInfo),
                        new CodePrimitiveExpression(scenario.Name),
                        tagsExpression)));

            AddLineDirective(testMethod.Statements, scenario);
            testMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    new CodeThisReferenceExpression(),
                    generationContext.ScenarioInitializeMethod.Name,
                    new CodeVariableReferenceExpression("scenarioInfo")));

            if (HasFeatureBackground(generationContext.Feature))
            {
                AddLineDirective(testMethod.Statements, generationContext.Feature.Background);
                testMethod.Statements.Add(
                    new CodeMethodInvokeExpression(
                        new CodeThisReferenceExpression(),
                        generationContext.FeatureBackgroundMethod.Name));
            }

            foreach (var scenarioStep in scenario.Steps)
            {
                GenerateStep(testMethod, scenarioStep, paramToIdentifier);
            }

            AddLineDirectiveHidden(testMethod.Statements);

            // call scenario cleanup
            testMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    new CodeThisReferenceExpression(),
                    generationContext.ScenarioCleanupMethod.Name));
        }

        private void SetupTestMethod(TestClassGenerationContext generationContext, CodeMemberMethod testMethod, ScenarioDefinition scenarioDefinition, IEnumerable<Tag> additionalTags, string variantName, string exampleSetIdentifier, bool rowTest = false)
        {
            testMethod.Attributes = MemberAttributes.Public;
            testMethod.Name=GetTestMethodName(scenarioDefinition, variantName, exampleSetIdentifier);
            var friendlyTestName = scenarioDefinition.Name;
            if (variantName != null)
                friendlyTestName = string.Format("{0}: {1}", scenarioDefinition.Name, variantName);

            if (rowTest)
                testGeneratorProvider.SetRowTest(generationContext, testMethod, friendlyTestName);
            else
                testGeneratorProvider.SetTestMethod(generationContext, testMethod, friendlyTestName);

            List<string> scenarioCategories;
            decoratorRegistry.DecorateTestMethod(generationContext, testMethod, ConcatTags(scenarioDefinition.Tags, additionalTags), out scenarioCategories);

            if (scenarioCategories.Any())
                testGeneratorProvider.SetTestMethodCategories(generationContext, testMethod, scenarioCategories);
        }

        private static string GetTestMethodName(ScenarioDefinition scenario, string variantName, string exampleSetIdentifier)
        {
            var methodName = string.Format(TEST_NAME_FORMAT, scenario.Name.ToIdentifier());
            if (variantName != null)
            {
                var variantNameIdentifier = variantName.ToIdentifier().TrimStart('_');
                methodName = string.IsNullOrEmpty(exampleSetIdentifier)
                    ? string.Format("{0}_{1}", methodName, variantNameIdentifier)
                    : string.Format("{0}_{1}_{2}", methodName, exampleSetIdentifier, variantNameIdentifier);
            }

            return methodName;
        }

        private IEnumerable<Tag> ConcatTags(params IEnumerable<Tag>[] tagLists)
        {
            return tagLists.Where(tagList => tagList != null).SelectMany(tagList => tagList);
        }

        private CodeExpression GetSubstitutedString(string text, ParameterSubstitution paramToIdentifier)
        {
            if (text == null)
                return new CodeCastExpression(typeof(string), new CodePrimitiveExpression(null));
            if (paramToIdentifier == null)
                return new CodePrimitiveExpression(text);

            Regex paramRe = new Regex(@"\<(?<param>[^\>]+)\>");
            string formatText = text.Replace("{", "{{").Replace("}", "}}");
            List<string> arguments = new List<string>();

            formatText = paramRe.Replace(formatText, match =>
                                                     {
                                                         string param = match.Groups["param"].Value;
                                                         string id;
                                                         if (!paramToIdentifier.TryGetIdentifier(param, out id))
                                                             return match.Value;
                                                         int argIndex = arguments.IndexOf(id);
                                                         if (argIndex < 0)
                                                         {
                                                             argIndex = arguments.Count;
                                                             arguments.Add(id);
                                                         }
                                                         return "{" + argIndex + "}";
                                                     });

            if (arguments.Count == 0)
                return new CodePrimitiveExpression(text);

            List<CodeExpression> formatArguments = new List<CodeExpression>();
            formatArguments.Add(new CodePrimitiveExpression(formatText));
            formatArguments.AddRange(arguments.Select(id => new CodeVariableReferenceExpression(id)).Cast<CodeExpression>());

            return new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression(typeof(string)),
                "Format",
                formatArguments.ToArray());
        }

        private void GenerateStep(CodeMemberMethod testMethod, Step gherkinStep, ParameterSubstitution paramToIdentifier)
        {
            var testRunnerField = GetTestRunnerExpression();
            var scenarioStep = AsSpecFlowStep(gherkinStep);

            //testRunner.Given("something");
            List<CodeExpression> arguments = new List<CodeExpression>();
            arguments.Add(
                GetSubstitutedString(scenarioStep.Text, paramToIdentifier));
            if (scenarioStep.Argument != null)
                AddLineDirectiveHidden(testMethod.Statements);
            arguments.Add(
                GetDocStringArgExpression(scenarioStep.Argument as DocString, paramToIdentifier));
            arguments.Add(
                GetTableArgExpression(scenarioStep.Argument as DataTable, testMethod.Statements, paramToIdentifier));
            arguments.Add(new CodePrimitiveExpression(scenarioStep.Keyword));

            AddLineDirective(testMethod.Statements, scenarioStep);
            testMethod.Statements.Add(
                new CodeMethodInvokeExpression(
                    testRunnerField,
                    scenarioStep.StepKeyword.ToString(),
                    arguments.ToArray()));
        }

        private SpecFlowStep AsSpecFlowStep(Step step)
        {
            var specFlowStep = step as SpecFlowStep;
            if (specFlowStep == null)
                throw new TestGeneratorException("The step must be a SpecFlowStep.");
            return specFlowStep;
        }

        private int tableCounter = 0;
        private CodeExpression GetTableArgExpression(DataTable tableArg, CodeStatementCollection statements, ParameterSubstitution paramToIdentifier)
        {
            if (tableArg == null)
                return new CodeCastExpression(typeof(Table), new CodePrimitiveExpression(null));

            tableCounter++;

            //TODO[Gherkin3]: remove dependency on having the first row as header
            var header = tableArg.Rows.First();
            var body = tableArg.Rows.Skip(1).ToArray();

            //Table table0 = new Table(header...);
            var tableVar = new CodeVariableReferenceExpression("table" + tableCounter);
            statements.Add(
                new CodeVariableDeclarationStatement(typeof(Table), tableVar.VariableName,
                    new CodeObjectCreateExpression(
                        typeof(Table),
                        GetStringArrayExpression(header.Cells.Select(c => c.Value), paramToIdentifier))));

            foreach (var row in body)
            {
                //table0.AddRow(cells...);
                statements.Add(
                    new CodeMethodInvokeExpression(
                        tableVar,
                        "AddRow",
                        GetStringArrayExpression(row.Cells.Select(c => c.Value), paramToIdentifier)));
            }
            return tableVar;
        }

        private CodeExpression GetDocStringArgExpression(DocString docString, ParameterSubstitution paramToIdentifier)
        {
            return GetSubstitutedString(docString == null ? null : docString.Content, paramToIdentifier);
        }

        #region Line pragma handling

        private void AddLinePragmaInitial(CodeTypeDeclaration testType, string sourceFile)
        {
            if (generatorConfiguration.AllowDebugGeneratedFiles)
                return;

            codeDomHelper.BindTypeToSourceFile(testType, Path.GetFileName(sourceFile));
        }

        private void AddLineDirectiveHidden(CodeStatementCollection statements)
        {
            if (generatorConfiguration.AllowDebugGeneratedFiles)
                return;

            codeDomHelper.AddDisableSourceLinePragmaStatement(statements);
        }

        private void AddLineDirective(CodeStatementCollection statements, Background background)
        {
            AddLineDirective(statements, background.Location);
        }

        private void AddLineDirective(CodeStatementCollection statements, ScenarioDefinition scenarioDefinition)
        {
            AddLineDirective(statements, scenarioDefinition.Location);
        }

        private void AddLineDirective(CodeStatementCollection statements, Step step)
        {
            AddLineDirective(statements, step.Location);
        }

        private void AddLineDirective(CodeStatementCollection statements, Location location)
        {
            if (location == null || generatorConfiguration.AllowDebugGeneratedFiles)
                return;

            codeDomHelper.AddSourceLinePragmaStatement(statements, location.Line, location.Column);
        }

        #endregion
    }
}
