# Moldinium

## Teaser

Moldinium allows you to write code in interfaces such as these:

```c#
interface IParentJob
{
    IList<IChildJob> Jobs { get; init; }

    String ParentName { get; set; }
}

interface IChildJob
{
    IOperationGroup Parent { get; init; }

    String ChildName { get; set; }

    String QualifiedName => $"{Parent.ParentName}.{ChildName}";
}
```

It will then create class implementations of these at runtime that

- implement `INotifyPropertyChanged` on for xaml-based UIs to bind against, or
- implement a different update notification mechanism for Blazor (see below), and
- have both update mechanisms trigger even for calculated properties
  (eg. `QualifiedName` will have a change trigger fired if *either* the child or the parent's namechanges)

## The three parts of Moldinium

Moldinium consists of three parts that are not dependent on each other and can be used in isolation,
but they shine brighter when used together. Those are

- the bakery: dynamic type creation
- the compositor: type and instance dependency injection
- the tracker: value dependency tracking

## Dependency tracking and the way this works on Blazor

Dependency tracking a way to track a computed expression to be notified when it's dependent
values change. In the teaser above, `QualifiedName` has the dependencies `ChildName`,
`Parent` and `Parent.ParentName`. So how can 