# Playground for experimentation with C-, a subset of the C# language

* Parameterized construction sucks, the best we got is having a record
  implement an interface and then construct that. The object can still
  be made polymorphic, for instance in the getter wrapper of the
  interface implementation.

* The rest works quite well, the difficulty here is finding a sensible
  subset to work with, there are a number of topics not yet explorered:
    
    * interface function implementation wrapping
    * nested containers
    * lifetime management

