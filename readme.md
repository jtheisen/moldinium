# Playground for experimentation with C-, a subset of the C# language

* C- can indeed bring back type safety by making dependency injection
  part of the build (in a "verify run" after the compile):

    * All type dependencies need to be known early as is DI-dogma anyway.
    * If this goes through, all further needed types can be constructed.
    * The construction in this playground is dynamic, but assembly
    weaving could also be used: Another reason why we want to know
    all the required types at build time.

* The current implementation constructs a (potentially) abstract class
  for each interface and then proxies that. The base is

    * necessary for baking in and merging the interface default
    implementations easily, the C# runtime does all that for us
    and DynamicProxy can't handle interface default implementations
    right now,

    * convenient for value-typed property implementations as they can
    then be fields in the constructed object,

    * still needs to be derived from again as a caching mechanism for
    methods cannot be implemented on the base directly without
    touching the auto-baking of the first point (or at least I
    don't know how).

* Parameterized construction sucks.

    * the best we got is having a record
      implement the interface and then construct that. The object can still
      be made polymorphic, for instance in the getter wrapper of the
      interface implementation.

    * It would be awesome if the C# language would allow the record cloning
    syntax for interfaces that have a suited cloning method. In the hope
    for it's arrival I think it's better to write domain model properties
    with init-setters even though at the moment those can't be called.

        * Even cooler would be `new MyInterface(...) { ... }` that gets translated
        to `Construct<MyInterface>(...)` but good luck getting that in.

* The rest works quite well, the difficulty here is finding a sensible
  subset to work with, there are a number of topics not yet explorered:
    
    * interface function implementation wrapping
    * nested containers
    * lifetime management

