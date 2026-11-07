using ClothingStore.Api.Data;
using ClothingStore.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Register SQL Server database access.
builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("StoreDatabase")
        ?? throw new InvalidOperationException(
            "The StoreDatabase connection string is missing."
        )
    )
);

// Limit incoming request sizes.
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024
);

var app = builder.Build();

// Photos stay on disk; their URLs are saved in SQL Server.
var imageDirectory = Path.Combine(
    app.Environment.ContentRootPath,
    "App_Data",
    "ProductImages"
);

Directory.CreateDirectory(imageDirectory);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Return all products, including their descriptive fields.
app.MapGet("/api/products", async (StoreDbContext db) =>
{
    var products = await db.Products
        .AsNoTracking()
        .Include(product => product.Details)
        .OrderBy(product => product.Id)
        .ToListAsync();

    return Results.Ok(products);
})
.WithName("GetProducts");

// Return one product.
app.MapGet("/api/products/{id:int}", async (int id, StoreDbContext db) =>
{
    var product = await db.Products
        .AsNoTracking()
        .Include(product => product.Details)
        .SingleOrDefaultAsync(product => product.Id == id);

    return product is null
        ? Results.NotFound()
        : Results.Ok(product);
});

// Upload a product photo.
app.MapPost("/api/product-images", async (HttpRequest request) =>
{
    const int maxBytes = 5 * 1024 * 1024;
    var contentType = request.ContentType?.ToLowerInvariant();

    if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
    {
        return Results.BadRequest(new
        {
            message = "Choose a JPEG, PNG, or WebP image."
        });
    }

    if (request.ContentLength > maxBytes)
    {
        return Results.BadRequest(new
        {
            message = "Photo must be no larger than 5 MB."
        });
    }

    using var buffer = new MemoryStream();
    var chunk = new byte[8192];
    int read;

    while ((read = await request.Body.ReadAsync(
        chunk,
        request.HttpContext.RequestAborted
    )) > 0)
    {
        if (buffer.Length + read > maxBytes)
        {
            return Results.BadRequest(new
            {
                message = "Photo must be no larger than 5 MB."
            });
        }

        await buffer.WriteAsync(chunk.AsMemory(0, read));
    }

    var bytes = buffer.ToArray();
    var extension = ProductImageFiles.DetectExtension(bytes);

    if (extension is null ||
        ProductImageFiles.ContentType(extension) != contentType)
    {
        return Results.BadRequest(new
        {
            message = "The file contents do not match a supported image type."
        });
    }

    var fileName = $"{Guid.NewGuid():N}{extension}";

    await File.WriteAllBytesAsync(
        Path.Combine(imageDirectory, fileName),
        bytes,
        request.HttpContext.RequestAborted
    );

    return Results.Ok(new
    {
        imageUrl = $"/api/product-images/{fileName}"
    });
});

// Serve an uploaded photo.
app.MapGet("/api/product-images/{fileName}",
    (string fileName, HttpContext context) =>
{
    if (!ProductImageFiles.IsSafeName(fileName))
        return Results.NotFound();

    var path = Path.Combine(imageDirectory, fileName);

    if (!File.Exists(path))
        return Results.NotFound();

    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    return Results.File(
        path,
        ProductImageFiles.ContentType(Path.GetExtension(fileName))
    );
});

// Validate and save a new product.
app.MapPost("/api/products",
    async (CreateProductRequest request, StoreDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) ||
        request.Name.Length > 120 ||
        string.IsNullOrWhiteSpace(request.Category) ||
        request.Category.Length > 80 ||
        string.IsNullOrWhiteSpace(request.Description) ||
        request.Description.Length > 2000)
    {
        return Results.BadRequest(new
        {
            message = "Provide a title (up to 120 characters), category (80), and description (2000)."
        });
    }

    if (request.Price <= 0 ||
        request.Price > 1000000 ||
        decimal.Round(request.Price, 2) != request.Price)
    {
        return Results.BadRequest(new
        {
            message = "Price must be between $0.01 and $1,000,000 with no more than two decimal places."
        });
    }

    var details = request.Details ?? [];

    if (details.Length > 20 || details.Any(detail =>
        detail is null ||
        string.IsNullOrWhiteSpace(detail.Name) ||
        string.IsNullOrWhiteSpace(detail.Value) ||
        detail.Name.Length > 60 ||
        detail.Value.Length > 200))
    {
        return Results.BadRequest(new
        {
            message = "Provide a name and value for each detail. Use at most 20 details, with names up to 60 and values up to 200 characters."
        });
    }

    var cleanDetails = details.Select(detail =>
        new ProductDetailEntity
        {
            Name = detail!.Name!.Trim(),
            Value = detail.Value!.Trim()
        }
    ).ToList();

    var uniqueNames = cleanDetails
        .Select(detail => detail.Name)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    if (uniqueNames != cleanDetails.Count)
    {
        return Results.BadRequest(new
        {
            message = "Use a different name for each descriptive field."
        });
    }

    string? imageUrl = null;
    var imageAlt = request.ImageAlt?.Trim() ?? "";

    if (!string.IsNullOrEmpty(request.ImageUrl))
    {
        const string prefix = "/api/product-images/";

        var fileName = request.ImageUrl.StartsWith(
            prefix,
            StringComparison.Ordinal
        )
            ? request.ImageUrl[prefix.Length..]
            : "";

        if (!ProductImageFiles.IsSafeName(fileName) ||
            !File.Exists(Path.Combine(imageDirectory, fileName)))
        {
            return Results.BadRequest(new
            {
                message = "Upload a valid product photo first."
            });
        }

        if (string.IsNullOrWhiteSpace(imageAlt) || imageAlt.Length > 250)
        {
            return Results.BadRequest(new
            {
                message = "Provide an image description up to 250 characters."
            });
        }

        imageUrl = request.ImageUrl;
    }

    var product = new ProductEntity
    {
        Name = request.Name.Trim(),
        Description = request.Description.Trim(),
        Category = request.Category.Trim(),
        Price = request.Price,
        Sizes = [],
        Colors = [],
        ImageUrl = imageUrl,
        ImageAlt = imageAlt,
        Details = cleanDetails
    };

    db.Products.Add(product);

    // Saves the product and its details; SQL Server assigns their IDs.
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Id}", product);
})
.WithName("CreateProduct");

// Validate a demo cart and calculate its total using database prices.
app.MapPost("/api/checkout",
    async (CheckoutRequest request, StoreDbContext db) =>
{
    if (request.Items is not { Length: > 0 })
    {
        return Results.BadRequest(new
        {
            message = "Your cart is empty."
        });
    }

    decimal total = 0;

    foreach (var item in request.Items)
    {
        if (item is null || item.Quantity is < 1 or > 99)
        {
            return Results.BadRequest(new
            {
                message = "Quantity must be between 1 and 99."
            });
        }

        var product = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == item.ProductId);

        if (product is null)
        {
            return Results.BadRequest(new
            {
                message = "A product is no longer available."
            });
        }

        var invalidSize = product.Sizes.Length > 0
            ? !product.Sizes.Contains(item.Size)
            : !string.IsNullOrEmpty(item.Size);

        var invalidColor = product.Colors.Length > 0
            ? !product.Colors.Contains(item.Color)
            : !string.IsNullOrEmpty(item.Color);

        if (invalidSize || invalidColor)
        {
            return Results.BadRequest(new
            {
                message = $"Invalid size or color for {product.Name}."
            });
        }

        total += product.Price * item.Quantity;
    }

    return Results.Ok(new
    {
        confirmationId = Guid.NewGuid(),
        total
    });
});

app.Run();

// Data accepted when creating a product.
record CreateProductRequest(
    string? Name,
    string? Category,
    decimal Price,
    string? Description,
    ProductDetailRequest?[]? Details,
    string? ImageUrl = null,
    string? ImageAlt = null
);

record ProductDetailRequest(string? Name, string? Value);

// Data accepted during demo checkout.
record CheckoutRequest(CheckoutItem?[]? Items);

record CheckoutItem(
    int ProductId,
    string Size,
    string Color,
    int Quantity
);

// Identify allowed image formats and reject unsafe filenames.
static class ProductImageFiles
{
    public static string? DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 24 &&
            bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }
            ) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return ".png";
        }

        if (bytes.Length >= 4 &&
            bytes[0] == 255 &&
            bytes[1] == 216 &&
            bytes[2] == 255 &&
            bytes[^2] == 255 &&
            bytes[^1] == 217)
        {
            return ".jpg";
        }

        if (bytes.Length >= 16 &&
            bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }

    public static bool IsSafeName(string name) =>
        Guid.TryParseExact(
            Path.GetFileNameWithoutExtension(name),
            "N",
            out _
        ) &&
        Path.GetFileName(name) == name &&
        Path.GetExtension(name) is ".jpg" or ".png" or ".webp";

    public static string ContentType(string extension) => extension switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}