namespace LogiTrack.Models
{
    public class ApiError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;

        public static ApiError Create(string code, string message, string traceId)
            => new()
            {
                Code = code,
                Message = message,
                TraceId = traceId
            };
    }
}
