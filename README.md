# specflow-retry
SpecFlow generator plugin that adds ability to retry tests on failure for MSTest/[NUnit](http://nunit.org/).

## Setup

To use this plugin with MsTest you should add following section into <specFlow></specFlow> in your App.config:

``` 
<unitTestProvider name="MsTest" />
<!-- if you will not set "unitTestProvider" that will use "nUnit" by default  -->
<plugins>
    <add name="SpecFlow.Retry.Generator" path="..\packages\SpecFlow.Retry.2.3.0\lib\net45" type="Generator" />
</plugins>
```

To use this plugin with nUnit you should add following section into <specFlow></specFlow> in your App.config:

``` 
<unitTestProvider name="nUnit" /> 
<!-- if you will not set "unitTestProvider" that will use "nUnit" by default  -->
<plugins>
    <add name="SpecFlow.Retry.Generator" path="..\packages\SpecFlow.Retry.2.3.0\lib\net45" type="Generator" />
</plugins>
```

## Usage

Adding a retry at the feature level will re-try any failing scenario:

```
@retry:1
Feature: SampleFeature
    In order to avoid silly mistakes
    As a math idiot
    I want to be told the sum of two numbers

Scenario: Tag on feature should be applied
    Then scenario should be run 2 times

Scenario Outline: Tag on feature should be applied to outlined scenario
    Then scenario "<example>" should be run 2 times
Examples: 
    | example |
    | first   |
    | second  |
```

Adding a retry at the scenario level will re-try the failing scenario:

```
@retry:2
Scenario: Random number generator test
    Given I have random number generator
    When it generates number
    Then I'll be lucky if it will be greater then 0.3
```

## Build status
Continious integration: [![Build status](https://ci.appveyor.com/api/projects/status/fgr338iwl0pnd8u0?svg=true)](https://ci.appveyor.com/project/DamirAinullin/specflow-retry)

[![NuGet version](https://badge.fury.io/nu/specflow.retry.svg)](https://badge.fury.io/nu/specflow.retry)

