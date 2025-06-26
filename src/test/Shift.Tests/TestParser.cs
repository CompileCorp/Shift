/*using System;
using System.IO;
using System.Linq;
using DmdSystem.Parsers;

namespace DmdSystem;

public class TestParser
{
    public static void TestDmdParsing()
    {
        Console.WriteLine("🧪 Testing DMD Parser");
        Console.WriteLine("=====================");
        Console.WriteLine();

        try
        {
            var dmdParser = new DmdParser();
            var dmdFiles = Directory.GetFiles(".", "*.dmd", SearchOption.TopDirectoryOnly);

            if (!dmdFiles.Any())
            {
                Console.WriteLine("❌ No .dmd files found in current directory.");
                return;
            }

            Console.WriteLine($"Found {dmdFiles.Length} DMD file(s):");
            foreach (var file in dmdFiles)
            {
                Console.WriteLine($"  - {Path.GetFileName(file)}");
            }
            Console.WriteLine();

            // Parse DMD files
            var targetModel = dmdParser.ParseFiles(dmdFiles);
            
            Console.WriteLine($"✅ Successfully parsed {targetModel.Tables.Count} tables:");
            Console.WriteLine();

            foreach (var table in targetModel.Tables)
            {
                Console.WriteLine($"📦 Table: {table.Name}");
                Console.WriteLine($"   Fields ({table.Fields.Count}):");
                foreach (var field in table.Fields)
                {
                    var nullable = field.IsNullable ? " (nullable)" : "";
                    Console.WriteLine($"     - {field.Name}: {field.Type}{nullable}");
                }

                if (table.ForeignKeys.Any())
                {
                    Console.WriteLine($"   Foreign Keys ({table.ForeignKeys.Count}):");
                    foreach (var fk in table.ForeignKeys)
                    {
                        var nullable = fk.IsNullable ? " (nullable)" : "";
                        Console.WriteLine($"     - {fk.TargetTable}: {fk.RelationshipType}{nullable}");
                    }
                }

                if (table.Indexes.Any())
                {
                    Console.WriteLine($"   Indexes ({table.Indexes.Count}):");
                    foreach (var index in table.Indexes)
                    {
                        var unique = index.IsUnique ? " (unique)" : "";
                        Console.WriteLine($"     - ({string.Join(", ", index.Fields)}){unique}");
                    }
                }

                if (table.Attributes.Any())
                {
                    Console.WriteLine($"   Attributes ({table.Attributes.Count}):");
                    foreach (var attr in table.Attributes)
                    {
                        Console.WriteLine($"     - @{attr.Key}: {attr.Value}");
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine("✅ DMD parsing test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during parsing: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner error: {ex.InnerException.Message}");
            }
        }
    }
} */