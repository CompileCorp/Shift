using Compile.Shift.Model;

namespace Compile.Shift.Tests;

public class ParserTests
{
    private readonly Parser _parser;

    public ParserTests()
    {
        _parser = new Parser();
    }

    [Fact]
    public void Parse_Field_With_Attributes()
    {
        var model = new DatabaseModel();
        var content = @"model Sample {
  string(20) Name @reducesize @allowdataloss
}";

        _parser.ParseTable(model, content);
        var table = model.Tables["Sample"];
        var field = table.Fields.First(f => f.Name == "Name");
        Assert.True(field.Attributes.ContainsKey("reducesize"));
        Assert.True(field.Attributes.ContainsKey("allowdataloss"));
    }
}