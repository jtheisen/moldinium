# CMinus/Moldinium

This repo is called CMinus but it's going to merge with my
old Moldinium project under the name Moldinium.

It's a library to write code entirely in C# interfaces and
consists of three independent parts:

- dynamic type creation from interfaces
- dependency injection
- dependency tracking

## State

The repo shows the creation of type from interfaces,
with dependencies resolved, and

- implemented properties wrapped as computed,
- methods wrapped as actions (but see below) and
- unimplemented properties implemented as trackable and
  - get resolved if not-nullable and with an init setter,
  - get default-initialized from a default provider if
    not-nullable and with a set setter

The Moldinium types can implement INotifyPropertyChanged
but this is only tried separately from the tracking
in the test suite as of yet.

## Todo

The following notes are for myself to remember what issues
still need tackling:

### Derived type's properties need to be derived themselves

This is necessary to allow tools that do reflection themselves,
such as EF or JsonConvert to construct the correct types
to put into properties.

### Derived types need to form a hierarchy

This is necessary for EF and serialization tools to understand
the hierarchy.

### Attributes need to be converted

For the same reasons

### Tracking needs an action context

Apparantly this wasn't done yet, so there's nothing to call
in the implementation of function wrappers.

### The bakery should support nested wrappers

Not strictly necessary, but this would be really useful.

### Internal dependency resolving

Not strictly necessary as long as the tracking repository
is statically located, as it currently still is, but it has
other advantages, such as that dependencies can be injected
into types created by EF or deserializers.

This requires the dependency resolver to be ambient.
