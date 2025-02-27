using Dicom;
using DICOM_Application.Model;
using Microsoft.AspNetCore.Mvc;

namespace DICOM_Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DicomController : Controller
    {
        private readonly IWebHostEnvironment _env;
        public DicomController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost, Route("UploadFile")]
        public async Task<JsonResponseModel> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new JsonResponseModel
                {
                    Errors = true,
                    Message = "No File Uploaded.",
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
            if (Path.GetExtension(file.FileName).ToLower() != ".dcm")
            {
                return new JsonResponseModel
                {
                    Errors = true,
                    Message = "Invalid file extenstion. Only .dcm file are allowed.",
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
            try
            {
                var dicomfile = DicomFile.Open(file.OpenReadStream());
                var dataset = dicomfile.Dataset;

                // Validate dicom mendatory fields 
                if (!dataset.TryGetSingleValue<string>(DicomTag.PatientID, out _) ||
                   !dataset.TryGetSingleValue<string>(DicomTag.StudyDescription, out _) ||
                   !dataset.TryGetSingleValue<string>(DicomTag.Modality, out _))
                {
                    return new JsonResponseModel
                    {
                        Errors = true,
                        Message = "Missing essential metadata fields",
                        StatusCode = StatusCodes.Status422UnprocessableEntity
                    };

                }
                // Store file in wwwroot/DicomFiles folder
                var uploadsFolder = Path.Combine(_env.WebRootPath, "DicomFiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filepath = Path.Combine(uploadsFolder, file.FileName);
                using (var stream = new FileStream(filepath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return new JsonResponseModel { Success = true, Message = "File Uploaded Successfully.", StatusCode = StatusCodes.Status200OK };
            }
            catch (Exception ex)
            {
                return new JsonResponseModel { Success = true, Message = $"invalid Dicom File :- {ex.Message}", StatusCode = StatusCodes.Status400BadRequest };
            }
        }
    }
}
