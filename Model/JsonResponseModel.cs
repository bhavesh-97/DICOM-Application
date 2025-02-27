namespace DICOM_Application.Model
{
    public class JsonResponseModel
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public Object? Data { get; set; }
        public int StatusCode { get; set; } = StatusCodes.Status400BadRequest;
        public object Errors { get; set; } = false;
    }
}
