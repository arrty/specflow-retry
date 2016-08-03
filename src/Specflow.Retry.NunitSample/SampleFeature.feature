@retry:1
Feature: SampleFeature
    In order to avoid silly mistakes
    As a math idiot
    I want to be told the sum of two numbers

@retry:2
Scenario: Random number generator test
    Given I have random number generator
    When it generates number
    Then I'll be lucky if it will be greater then 0.3

Scenario: Scenario context isolation
    Given by default scenario context stored value is 0
    When I increment scenario context stored value
    Then scenario context stored value should be 1
    And scenario should be run 2 times

Scenario: Tag on feature should be applied
    Then scenario should be run 2 times

@retry:2
Scenario: Tag on scenario is preffered
    Then scenario should be run 3 times

#@retryExcept:Specflow.Retry.NunitSample.CriticalException
#@criticalException
#Scenario: On except exception should not retry
#    When "Specflow.Retry.NunitSample.CriticalException" thrown
#    Then nothing

Scenario Outline: Tag on feature should be applied to outlined scenario
    Then scenario "<example>" should be run 2 times
Examples: 
    | example |
    | first   |
    | second  |

