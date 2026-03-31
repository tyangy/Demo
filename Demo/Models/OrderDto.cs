namespace Demo.Models
{
    // DTO定义
    public class CreateOrderRequest
    {
        public string UserId { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class BatchOrderRequest
    {
        public List<string> OrderIds { get; set; } = new();
        public string Operation { get; set; } = "process";
    }

    public class BatchResult
    {
        public string OrderId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public object? Data { get; set; }
        public string? Error { get; set; }
    }
}
