module ExampleTest

open Xunit

open IfSharp.Kernel

[<Fact>]
let ``Trivial example test``() =

    let asSvg = Util.Svg "test"
    Assert.Equal("test", asSvg.Svg)