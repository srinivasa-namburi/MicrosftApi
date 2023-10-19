namespace BlazorAddIn.Models
{
    
    public enum Direction
    {
        To,
        From
    }
    public class Message
    {
        public string? Text { get; set; } = string.Empty;
        public Direction MessageDirection { get; set; } = Direction.From;
    }
}
