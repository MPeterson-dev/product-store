namespace ClothingStore.Api.Models;

public class ProductEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }

    public string[] Sizes { get; set; } = [];
    public string[] Colors { get; set; } = [];

    public string? ImageUrl { get; set; }
    public string ImageAlt { get; set; } = "";

    public List<ProductDetailEntity> Details { get; set; } = [];
}

public class ProductDetailEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}