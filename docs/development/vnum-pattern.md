# Vnum Pattern - Enumeration Classes

The **Vnum** (Value Backed Enumeration) pattern provides a robust alternative to traditional enums. It's inspired by Jimmy Bogard's "Enumeration Classes" pattern and is used extensively throughout the system for type-safe, extensible enumeration-like constructs.

> ℹ️ **Where it comes from:** `Vnum` is provided by the **`CompileCorp.Vnum`** NuGet package (namespace `Compile.VnumEnumeration`). It is not defined in this repository.

> 📚 **In a hurry?** Jump to the [Quick Reference](#quick-reference) below for copy-paste snippets and common operations.

## Overview

Vnum provides a strongly-typed way to represent fixed sets of values with additional metadata, going beyond the limitations of traditional C# enums. It combines the benefits of enums with the flexibility of classes.

### Key Benefits

1. **Type Safety**: Compile-time checking and IntelliSense support
2. **Extensibility**: Can add properties, methods, and behavior
3. **Rich Metadata**: Support for descriptions, codes, and custom properties
4. **API Integration**: Easy serialization to DTOs for frontend consumption
5. **Testing**: Comprehensive testing utilities and patterns
6. **Performance**: Thread-safe caching with reflection optimization

## Quick Reference

A compact cheat sheet for everyday use. The sections further down explain each piece in depth, with design rationale and more examples.

> 📝 The `Status`/`StatusId` types below are **illustrative**. The real Vnums in this codebase are `DmdFieldType`, `SqlFieldType` (`src/Shift/Model/Vnums/`), and `CliCmd`, `CliCmdAlias`, `CliSubCmd` (`src/Shift.Cli/Vnums/`).

**Define an enum-backed Vnum (the recommended form):**

```csharp
public enum StatusId { Active = 1, Inactive = 2, Pending = 3 }

public sealed class Status : Vnum<StatusId>
{
    public Status() { }
    private Status(StatusId id, string code) : base(id, code) { }

    public static readonly Status Active   = new(StatusId.Active, "ACTIVE");
    public static readonly Status Inactive = new(StatusId.Inactive, "INACTIVE");
    public static readonly Status Pending  = new(StatusId.Pending, "PENDING");
}
```

See [Implementation Patterns](#implementation-patterns) for the simple (no-enum) and rich-metadata variants.

**Common operations:**

```csharp
// Enumerate
var all      = Vnum.GetAll<Status>();
var filtered = Vnum.GetAll<Status>(s => s.Code.Contains("ACTIVE"));

// Look up (throws if not found)
var a = Vnum.FromValue<Status>(1);
var b = Vnum.FromCode<Status>("ACTIVE");
var c = Vnum.FromCode<Status>("active", ignoreCase: true);
var d = Vnum.FromEnum<Status, StatusId>(StatusId.Active);

// Look up (safe, no throw)
Vnum.TryFromValue<Status>(1, out var s1);
Vnum.TryFromCode<Status>("active", ignoreCase: true, out var s2);
Vnum.TryFromEnum<Status, StatusId>(StatusId.Active, out var s3);

// Compare
if (status == Status.Active) { }
if (status.Code == "ACTIVE") { }
if (status.Value == 1) { }
```

**JSON (a Vnum serializes as its `Code` string):**

```csharp
var options = new JsonSerializerOptions { Converters = { new VnumJsonConverterFactory() } };

JsonSerializer.Serialize(Status.Active, options);            // "ACTIVE"
JsonSerializer.Deserialize<Status>("\"ACTIVE\"", options);   // Status.Active (case-sensitive)
```

See [JSON Serialization](#json-serialization) for collections, complex objects, and error handling.

**Testing:** derive a test class from `UnitTestContext<VnumTestingHelper<TVnum, TEnum>>` and call the helper assertions (`Vnum_Instances_Must_Have_Unique_Values()`, `All_Vnum_Instances_Must_Have_Matching_Enum()`, …) — see [Testing Patterns](#testing-patterns).

## Core Architecture

### Base Classes

#### `Vnum` (Base Class)
```csharp
public abstract class Vnum : IEquatable<Vnum>
{
    public long Value { get; }          // Numeric identifier
    public string Code { get; }         // String code identifier

    // Core functionality (filtering predicate is optional)
    public static IEnumerable<TVnum> GetAll<TVnum>(Func<TVnum, bool>? predicate = null) where TVnum : Vnum
    public static TVnum FromValue<TVnum>(long value) where TVnum : Vnum
    public static TVnum FromCode<TVnum>(string code, bool ignoreCase = false) where TVnum : Vnum
    public static bool TryFromValue<T>(long value, out T vnum) where T : Vnum
    public static bool TryFromCode<TVnum>(string code, out TVnum vnum) where TVnum : Vnum
    public static bool TryFromCode<TVnum>(string code, bool ignoreCase, out TVnum vnum) where TVnum : Vnum

    // Enum-specific lookups (defined on the base class; constrained to Vnum<TEnum>)
    public static TVnum FromEnum<TVnum, TEnum>(TEnum value)
        where TVnum : Vnum<TEnum> where TEnum : struct, Enum
    public static bool TryFromEnum<TVnum, TEnum>(TEnum value, out TVnum vnum)
        where TVnum : Vnum<TEnum> where TEnum : struct, Enum

    // Value-based equality and operators
    public bool Equals(Vnum? other);
    public static bool operator ==(Vnum? left, Vnum? right);
    public static bool operator !=(Vnum? left, Vnum? right);
}
```

> **Note:** The lookup methods are constrained only by `where TVnum : Vnum` — there is **no `new()` constraint**. A public parameterless constructor is a convention used by some implementations, but it is not required by the API.

#### `Vnum<TEnum>` (Generic Base Class)
```csharp
public abstract class Vnum<TEnum> : Vnum where TEnum : struct, Enum
{
    public TEnum Id { get; }   // The enum value, derived from Value

    // Two protected constructors are available:
    protected Vnum(long value, string code);
    protected Vnum(TEnum value, string code);
}
```

> `FromEnum` / `TryFromEnum` are **static methods on the base `Vnum` class** (shown above), not declared on `Vnum<TEnum>`. They are constrained to `Vnum<TEnum>` so they only apply to enum-backed Vnums.

## Implementation Patterns

> 📝 **Note on examples:** The Vnum types used throughout this guide (`Department`, `Country`, `ProductType`, `ProductCategory`, `Status`, etc.) are **illustrative** and do not exist in this repository. The real Vnums in this codebase are `DmdFieldType`, `SqlFieldType` (`src/Shift/Model/Vnums/`), and `CliCmd`, `CliCmdAlias`, `CliSubCmd` (`src/Shift.Cli/Vnums/`).

### Pattern 1: Simple Vnum (No Enum)

```csharp
public class PolicyType : Vnum
{
    public string Description { get; }
    
    public PolicyType() { }
    private PolicyType(int value, string code, string description) : base(value, code)
    {
        Description = description;
    }
    
    public static readonly PolicyType Standard = new(1, "STANDARD", "Standard Policy");
    public static readonly PolicyType Premium = new(2, "PREMIUM", "Premium Policy");
    public static readonly PolicyType Enterprise = new(3, "ENTERPRISE", "Enterprise Policy");
}
```

### Pattern 2: Vnum with Enum (Recommended)

```csharp
public enum DepartmentId
{
    Forestry = 1,
    Fishery = 2,
    Agriculture = 3,
}

public sealed class Department : Vnum<DepartmentId>
{
    public Department() { }
    private Department(DepartmentId id, string code) : base(id, code) { }

    public static readonly Department Forestry = new(DepartmentId.Forestry, "Forestry");
    public static readonly Department Fishery = new(DepartmentId.Fishery, "Fishery");
    public static readonly Department Agriculture = new(DepartmentId.Agriculture, "Agriculture");
}
```

### Pattern 3: Complex Vnum with Rich Metadata

```csharp
public class ProductType : Vnum<ProductTypeId>
{
    public string Description { get; }
    public string CategoryCode { get; }

    public ProductType() { }
    private ProductType(int value, string code, string description, string categoryCode) 
        : base(value, code)
    {
        Description = description;
        CategoryCode = categoryCode;
    }

    public static readonly ProductType StandardWidget = new(
        value: 110,
        code: "STD-WDG",
        description: "Standard Widget",
        categoryCode: "WDG"
    );

    public static readonly ProductType PremiumWidget = new(
        value: 120,
        code: "PRM-WDG", 
        description: "Premium Widget",
        categoryCode: "WDG"
    );
}
```

## Usage Examples

### Basic Operations

```csharp
// Get all instances
var allDepartments = Vnum.GetAll<Department>();

// Find by value
var department = Vnum.FromValue<Department>(1);
var department = Vnum.FromEnum<Department, DepartmentId>(DepartmentId.Forestry);

// Find by code
var department = Vnum.FromCode<Department>("Forestry");

// Safe operations
if (Vnum.TryFromValue<Department>(1, out var department))
{
    Console.WriteLine($"Found department: {department.Code}");
}
```

### Filtering with Predicates

The `GetAll<T>()` method supports optional predicate filtering for efficient querying:

```csharp
// Get all departments (no filter)
var allDepartments = Vnum.GetAll<Department>();

// Filter by code pattern
var matchingDepartments = Vnum.GetAll<Department>(d => d.Code.Contains("Department"));
// Returns: [Forestry, Fishery, Agriculture]

// Filter by multiple criteria
var activeAustralianPorts = Vnum.GetAll<Port>(p => 
    p.Country == Country.Australia && 
    p.Code.StartsWith("AU"));

// Filter by ID collection (useful for database queries)
var categoryIds = new List<ProductCategoryId> { 
    ProductCategoryId.Electronics, 
    ProductCategoryId.Furniture 
};
var selectedCategories = Vnum.GetAll<ProductCategory>(pc => 
    categoryIds.Contains(pc.Id));
// Returns: [Electronics, Furniture]

// Filter by property value
var widgetProducts = Vnum.GetAll<ProductType>(pt => 
    pt.Code.Contains("WDG"));

// Complex filtering
var widgetProducts = Vnum.GetAll<ProductType>(pt => 
    pt.CategoryCode == "WDG" && 
    pt.Description.Contains("Widget"));
```

**Performance Note:** Predicate filtering is efficient - it uses cached reflection results and evaluates the predicate in-memory. Prefer using `GetAll<T>(predicate)` over manually filtering with LINQ for better readability.

### In Business Logic

```csharp
public class ProductService
{
    public bool IsValidProductType(string productTypeCode)
    {
        return Vnum.TryFromCode<ProductType>(productTypeCode, out _);
    }
    
    public ProductType GetProductType(string code)
    {
        return Vnum.FromCode<ProductType>(code);
    }
    
    public IEnumerable<ProductType> GetWidgetProducts()
    {
        return Vnum.GetAll<ProductType>(pt => pt.Code.Contains("WDG"));
    }
}
```

### Real-World Example: Repository Pattern

A common pattern is converting database enum IDs back to Vnum instances:

```csharp
public async Task<List<ProductCategory>> GetActiveProductCategoriesAsync(
    Department department,
    Region region,
    CancellationToken cancellationToken = default)
{
    // Step 1: Query database for category IDs
    var categoryIds = await dbContext.ProductCategoryConfigs
        .Where(d => 
            d.DepartmentId == department.Id &&
            (d.RegionId == null || d.RegionId == region.Id) &&
            d.ActiveConfigVersionId != null)
        .Include(d => d.ActiveConfigVersion)
        .Where(d =>
            !d.ActiveConfigVersion!.IsDraft &&
            d.ActiveConfigVersion.EffectiveFrom <= DateTime.UtcNow &&
            (d.ActiveConfigVersion.EffectiveTo == null || 
             d.ActiveConfigVersion.EffectiveTo > DateTime.UtcNow))
        .Select(d => d.CategoryId)
        .Distinct()
        .ToListAsync(cancellationToken);

    // Step 2: Convert enum IDs to Vnum instances using predicate
    // ✅ Clean and efficient - single line
    return Vnum.GetAll<ProductCategory>(pc => categoryIds.Contains(pc.Id)).ToList();
    
    // ❌ Avoid this verbose approach:
    // var allCategories = Vnum.GetAll<ProductCategory>().ToDictionary(pc => pc.Id);
    // return categoryIds
    //     .Where(id => allCategories.ContainsKey(id))
    //     .Select(id => allCategories[id])
    //     .ToList();
}
```

**Benefits:**
- ✅ Type-safe conversion from database IDs to domain objects
- ✅ Single-pass filtering with predicate
- ✅ No intermediate collections or dictionaries
- ✅ Clear, readable intent

### In DTOs and API Contracts

```csharp
public class NewProductDto
{
    [Required]
    public string ProductCode { get; set; }
    
    /// <summary>
    /// Code binding to <see cref="ProductType"/>
    /// </summary>
    [Required]
    public string ProductType { get; set; }
    
    /// <summary>
    /// Code binding to <see cref="ProductCategory"/>
    /// </summary>
    [Required]
    public string Category { get; set; }
}
```

### In Blazor Components

You can project Vnum instances into your own simple DTO for binding. Define a record that suits your view, then map from `Code` and any extra properties:

```csharp
// A view-specific DTO you define in your own project
public record ProductTypeOption(string Code, string Description);

@code {
    private List<ProductTypeOption> productTypes = new();

    protected override Task OnInitializedAsync()
    {
        productTypes = Vnum.GetAll<ProductType>()
            .Select(pt => new ProductTypeOption(pt.Code, pt.Description))
            .ToList();

        return Task.CompletedTask;
    }
}
```

## Testing Patterns

### Basic Vnum Testing

```csharp
[TestFixture]
public class DepartmentTests
{
    [Test]
    public void Department_Should_Have_Correct_Values()
    {
        Department.Forestry.Value.Should().Be(1);
        Department.Forestry.Code.Should().Be("Forestry");
    }
    
    [Test]
    public void FromValue_Should_Return_Correct_Department()
    {
        var department = Vnum.FromValue<Department>(1);
        department.Should().Be(Department.Forestry);
    }
    
    [Test]
    public void FromCode_Should_Return_Correct_Department()
    {
        var department = Vnum.FromCode<Department>("Forestry");
        department.Should().Be(Department.Forestry);
    }
}
```

### Using Vnum Testing Helpers

```csharp
[TestFixture]
public class DepartmentTests : UnitTestContext<VnumTestingHelper<Department, DepartmentId>>
{
    [Test]
    public void All_Vnum_Instances_Must_Have_Matching_Enum()
    {
        Sut.All_Vnum_Instances_Must_Have_Matching_Enum();
    }

    [Test]
    public void All_Enum_Instances_Must_Convert_To_Vnum()
    {
        Sut.All_Enum_Instances_Must_Convert_To_Vnum();
    }
}
```

### Comprehensive Vnum Testing

```csharp
[TestFixture]
public class ProductTypeTests
{
    [Test]
    public void GetAll_Should_Return_All_Product_Types()
    {
        var allTypes = Vnum.GetAll<ProductType>().ToList();
        
        allTypes.Should().Contain(ProductType.StandardWidget);
        allTypes.Should().Contain(ProductType.PremiumWidget);
        allTypes.Should().HaveCountGreaterThan(0);
    }
    
    [Test]
    public void Filtering_Should_Work_Correctly()
    {
        var widgetTypes = Vnum.GetAll<ProductType>(pt => pt.Code.Contains("WDG"));
        
        widgetTypes.Should().Contain(ProductType.StandardWidget);
        widgetTypes.Should().Contain(ProductType.PremiumWidget);
        widgetTypes.Should().NotContain(ProductType.BasicGadget);
    }
    
    [Test]
    public void Equality_Should_Work_Correctly()
    {
        var type1 = ProductType.StandardWidget;
        var type2 = Vnum.FromCode<ProductType>("STD-WDG");
        
        type1.Should().Be(type2);
        type1.GetHashCode().Should().Be(type2.GetHashCode());
    }
}
```

## Best Practices

### 1. Naming Conventions

```csharp
// ✅ Good: Clear, descriptive names
public enum DepartmentId { Forestry = 1, Fishery = 2 }
public class Department : Vnum<DepartmentId> { }

// ❌ Bad: Unclear names
public enum Dept { D1 = 1, D2 = 2 }
public class Dept : Vnum<Dept> { }
```

### 2. Value Assignment

```csharp
// ✅ Good: Use constants for values
public static class ProductTypeConstants
{
    public static class StandardWidget
    {
        public const int Value = 110;
        public const string Code = "STD-WDG";
    }
}

// ✅ Good: Reference constants in enum
public enum ProductTypeId
{
    StandardWidget = ProductTypeConstants.StandardWidget.Value,
    PremiumWidget = ProductTypeConstants.PremiumWidget.Value,
}
```

### 3. Constructor Patterns

```csharp
// ✅ Good: Private constructor with public static readonly instances
public class Department : Vnum<DepartmentId>
{
    public Department() { } // Required for reflection
    private Department(DepartmentId id, string code) : base(id, code) { }
    
    public static readonly Department Forestry = new(DepartmentId.Forestry, "Forestry");
}
```

### 4. Additional Properties

```csharp
// ✅ Good: Add meaningful properties
public class ProductType : Vnum<ProductTypeId>
{
    public string Description { get; }
    public string CategoryCode { get; }
    
    // Constructor includes all properties
    private ProductType(int value, string code, string description, string categoryCode) 
        : base(value, code)
    {
        Description = description;
        CategoryCode = categoryCode;
    }
}
```

### 5. Testing Requirements

```csharp
// ✅ Good: Always test Vnum implementations
[TestFixture]
public class YourVnumTests
{
    [Test]
    public void All_Instances_Should_Be_Unique()
    {
        var instances = Vnum.GetAll<YourVnum>();
        var values = instances.Select(x => x.Value).ToList();
        var codes = instances.Select(x => x.Code).ToList();
        
        values.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyHaveUniqueItems();
    }
    
    [Test]
    public void FromValue_Should_Work_For_All_Instances()
    {
        foreach (var instance in Vnum.GetAll<YourVnum>())
        {
            var found = Vnum.FromValue<YourVnum>(instance.Value);
            found.Should().Be(instance);
        }
    }
}
```

## Common Vnum Types

### Core Business Types
- **Department**: Forestry, Fishery, Agriculture
- **ProductType**: StandardWidget, PremiumWidget, BasicGadget, etc.
- **ProductCategory**: Electronics, Furniture, Supplies, etc.
- **Country**: AU, US, CN, etc.

### Status Types
- **JobStatus**: Pending, InProgress, Completed, Failed
- **OfferStatus**: Draft, Submitted, Accepted, Rejected
- **TriangulationStatus**: NotStarted, InProgress, Completed

### Configuration Types
- **AddressType**: Postal, Location
- **ContactType**: Email, Phone, Mobile
- **SubscriberPropertyType**: Website, Phone, Address

## Performance Considerations

### Thread-Safe Caching
Vnum uses a thread-safe cache to store reflection results:

```csharp
private static readonly ConcurrentDictionary<Type, object[]> _cache = new();
```

### Reflection Optimization
- Reflection is only used once per type
- Results are cached for subsequent calls
- Only public static fields are considered

### Memory Efficiency
- Static readonly instances are created once
- No additional memory allocation during runtime
- Efficient equality comparison based on Value

## Migration from Enums

### Before (Traditional Enum)
```csharp
public enum ProductType
{
    StandardWidget = 110,
    PremiumWidget = 120
}

// Usage
if (productType == ProductType.StandardWidget) { }
```

### After (Vnum)
```csharp
public class ProductType : Vnum<ProductTypeId>
{
    public string Description { get; }
    
    public static readonly ProductType StandardWidget = new(110, "STD-WDG", "Standard Widget");
}

// Usage
if (productType == ProductType.StandardWidget) { }
if (productType.Code == "STD-WDG") { }
if (productType.Description.Contains("Widget")) { }
```

## JSON Serialization

### VnumJsonConverter

A custom JSON converter serializes Vnums to their `Code` string value, making JSON payloads compact and human-readable.

#### Configuration

```csharp
using System.Text.Json;

// Configure JSON options with Vnum converter
var jsonOptions = new JsonSerializerOptions
{
    Converters = { new VnumJsonConverterFactory() }
};
```

#### Serialization Behavior

```csharp
// Serialize Vnum to Code string
var department = Department.Forestry;
var json = JsonSerializer.Serialize(department, jsonOptions);
// Result: "Forestry"

// Deserialize from Code string (case-sensitive)
var department = JsonSerializer.Deserialize<Department>("\"Forestry\"", jsonOptions);  // ✅ Works
var department = JsonSerializer.Deserialize<Department>("\"forestry\"", jsonOptions);  // ❌ Throws JsonException

// Backward compatibility - deserialize from numeric value
var department = JsonSerializer.Deserialize<Department>("1", jsonOptions);
// Result: Department.Forestry
```

#### Complex Objects

```csharp
public class ProductConfiguration
{
    public Department Department { get; set; }
    public Country Country { get; set; }
    public ProductCategory Category { get; set; }
    public ProductStatus Status { get; set; }
}

var config = new ProductConfiguration
{
    Department = Department.Forestry,
    Country = Country.Australia,
    Category = ProductCategory.Electronics,
    Status = ProductStatus.Active
};

var json = JsonSerializer.Serialize(config, jsonOptions);
```

**Serialized JSON (compact and readable):**
```json
{
  "Department": "Forestry",
  "Country": "AU",
  "Category": "Electronics",
  "Status": "Active"
}
```

**Without VnumJsonConverter (verbose):**
```json
{
  "Department": { "Id": 1, "Value": 1, "Code": "Forestry" },
  "Country": { "Id": 36, "Value": 36, "Code": "AU" },
  "Category": { "Id": 290, "Value": 290, "Code": "Electronics", "DisplayName": "Electronics", "Description": "..." },
  "Status": { "Id": 2, "Value": 2, "Code": "Active", "Description": "..." }
}
```

#### Collections

```csharp
var categories = new List<ProductCategory>
{
    ProductCategory.Electronics,
    ProductCategory.Furniture,
    ProductCategory.Supplies
};

var json = JsonSerializer.Serialize(categories, jsonOptions);
// Result: ["Electronics", "Furniture", "Supplies"]
```

#### Benefits

1. **Compact JSON**: 70-80% smaller payload size
2. **Human-readable**: Codes instead of numeric IDs and full objects
3. **Case-sensitive**: Enforces exact code matching for data integrity
4. **Backward compatible**: Accepts numeric values for migration scenarios
5. **Type-safe**: Automatically reconstructs proper Vnum instances
6. **Universal**: Works for all Vnum types via `VnumJsonConverterFactory`

#### Error Handling

```csharp
// Invalid code throws JsonException
var json = "\"INVALID_CODE\"";
var department = JsonSerializer.Deserialize<Department>(json, jsonOptions);
// Throws: JsonException wrapping: 'INVALID_CODE' is not a valid code in Department

// Null handling
Department? nullDepartment = null;
var json = JsonSerializer.Serialize(nullDepartment, jsonOptions);
// Result: null

var deserialized = JsonSerializer.Deserialize<Department?>("null", jsonOptions);
// Result: null
```

## ASP.NET Core API Integration

### Automatic Vnum Serialization

APIs can be configured to automatically serialize and deserialize Vnums without manual conversion. This enables you to use Vnums directly in request/response DTOs and handler parameters.

#### API Configuration

Configure JSON serialization in your API startup (e.g., `ServiceCollectionExtensions.cs`):

```csharp
services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Register VnumJsonConverterFactory for automatic Vnum serialization
        options.JsonSerializerOptions.Converters.Add(new VnumJsonConverterFactory());
    });
```

#### Using Vnums in DTOs

```csharp
// DTO with Vnum properties
public record ProductConfigDto
{
    public ProductCategory? Category { get; init; }
    public ProductStatus? Status { get; init; }
    public Department? Department { get; init; }
    public Country? Country { get; init; }
}

// API Controller
[ApiController]
[Route("api/configs")]
public class ConfigsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateConfig([FromBody] ProductConfigDto dto)
    {
        // Vnums are automatically deserialized from string codes
        // No manual conversion needed!
        
        if (dto.Category == ProductCategory.Electronics)
        {
            // Work with strongly-typed Vnum instances
        }
        
        return Ok(dto); // Automatically serialized back to codes
    }
}
```

#### Request Example

**POST /api/configs** with JSON body:
```json
{
  "category": "Electronics",
  "status": "Active",
  "department": "Forestry",
  "country": "AU"
}
```

The API automatically:
1. ✅ Deserializes string codes to Vnum instances (case-sensitive)
2. ✅ Validates codes (throws `400 Bad Request` for invalid codes)
3. ✅ Provides strongly-typed Vnum objects in controllers/handlers
4. ✅ Serializes Vnums back to string codes in responses

#### Using Vnums in MediatR Handlers

```csharp
// Request with Vnum
public record CreateConfigRequest(
    Department Department,
    ProductCategory Category,
    Country? Country = null
) : IRequest<CreateConfigResponse>;

// Handler - no manual string conversion needed!
internal class CreateConfigHandler : IRequestHandler<CreateConfigRequest, CreateConfigResponse>
{
    public async Task<CreateConfigResponse> Handle(
        CreateConfigRequest request,
        CancellationToken cancellationToken)
    {
        // Work directly with Vnums
        if (request.Department == Department.Forestry)
        {
            // Type-safe operations
        }
        
        var config = new ConfigEntity
        {
            DepartmentId = request.Department.Value,    // Get numeric ID
            DepartmentCode = request.Department.Code,   // Get string code
            CategoryId = request.Category.Id            // Get enum ID
        };
        
        await repository.SaveAsync(config, cancellationToken);
        
        return new CreateConfigResponse(Success: true);
    }
}
```

#### Before and After API Configuration

**Before (Manual Conversion Required):**
```csharp
// DTO with string codes
public record ConfigDto
{
    public string Department { get; init; }
    public string Category { get; init; }
}

// Controller - manual conversion everywhere
[HttpPost]
public async Task<IActionResult> CreateConfig([FromBody] ConfigDto dto)
{
    // Manual conversion required ❌
    if (!Vnum.TryFromCode<Department>(dto.Department, out var department))
    {
        return BadRequest("Invalid department");
    }
    
    if (!Vnum.TryFromCode<ProductCategory>(dto.Category, out var category))
    {
        return BadRequest("Invalid category");
    }
    
    // Now work with Vnums...
    var request = new CreateConfigRequest(department, category);
}
```

**After (Automatic Conversion):**
```csharp
// DTO with Vnum types
public record ConfigDto
{
    public Department Department { get; init; }
    public ProductCategory Category { get; init; }
}

// Controller - automatic conversion ✅
[HttpPost]
public async Task<IActionResult> CreateConfig([FromBody] ConfigDto dto)
{
    // Vnums are already validated and converted!
    // Work directly with strongly-typed instances
    var request = new CreateConfigRequest(dto.Department, dto.Category);
}
```

### Error Handling

When invalid Vnum codes are provided to the API, ASP.NET Core automatically catches the `JsonException` thrown by the `VnumJsonConverter` during model binding and returns a standardized error response.

#### Invalid Vnum Code

**Request:**
```json
POST /api/configs
{
  "department": "INVALID_DEPARTMENT",
  "category": "Electronics"
}
```

**Response: `400 Bad Request`**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-84c1fd4063c38d9f3900d06e56542d48-85d1d4-00",
  "errors": {
    "$.department": [
      "The JSON value could not be converted to Department. 'INVALID_DEPARTMENT' is not a valid code in Department"
    ]
  }
}
```

> The underlying lookup throws `InvalidOperationException` with the message `'{value}' is not a valid code in {TypeName}` (no list of valid codes is included), which the JSON converter surfaces as a `JsonException`.

#### Case Sensitivity

Vnum deserialization is **case-sensitive** by default:

**Request:**
```json
{
  "department": "forestry"
}
```

**Response: `400 Bad Request`**
```json
{
  "errors": {
    "$.department": [
      "The JSON value could not be converted to Department. 'forestry' is not a valid code in Department"
    ]
  }
}
```

✅ **Correct:** `"Forestry"` (exact case match)  
❌ **Incorrect:** `"forestry"`, `"FORESTRY"`

#### Benefits

1. ✅ **Automatic Validation**: No manual validation needed in controllers/handlers
2. ✅ **Consistent Error Format**: RFC 7807 ProblemDetails format
3. ✅ **Clear Error Messages**: Specifies the invalid code and Vnum type
4. ✅ **Proper HTTP Status**: `400 Bad Request` for invalid input
5. ✅ **JSON Path Context**: Shows exactly which field has the error (e.g., `$.department`)

The API automatically returns a properly formatted validation error without requiring manual validation in controllers or handlers.

## Integration with Entity Framework

### Storing Vnum References

```csharp
public class Product
{
    public int Id { get; set; }
    public string ProductCode { get; set; }
    public string ProductTypeCode { get; set; } // Store as string code
    
    // Navigation property (computed)
    public ProductType ProductType => Vnum.FromCode<ProductType>(ProductTypeCode);
}
```

## Summary

The Vnum pattern is a powerful, type-safe alternative to traditional enums that provides:

1. **Rich Metadata**: Descriptions, codes, and custom properties
2. **Type Safety**: Compile-time checking and IntelliSense
3. **Extensibility**: Easy to add new properties and methods
4. **JSON Serialization**: Compact, human-readable JSON with automatic conversion
5. **ASP.NET Core Integration**: Automatic serialization/deserialization in APIs
6. **Testing**: Comprehensive testing utilities
7. **Performance**: Thread-safe caching and optimization
8. **EF Core Integration**: Seamless storage and retrieval

It's used for representing fixed sets of business values with additional context and metadata, making the codebase more maintainable, the API more intuitive, and the developer experience more productive.
