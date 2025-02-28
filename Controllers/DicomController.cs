using Dicom;
using Dicom.Network;
using DICOM_Application.Model;
using Microsoft.AspNetCore.Mvc;

namespace DICOM_Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DicomController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DicomController> _logger;
        public DicomController(IWebHostEnvironment env, ILogger<DicomController> logger)
        {
            _env = env;
            _logger = logger;
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

                // Log the tags for varify their values
                _logger.LogInformation("PatientID: {PatientID}", dicomfile.Dataset.GetSingleValue<string>(DicomTag.PatientID));
                _logger.LogInformation("StudyDescription: {StudyDescription}", dicomfile.Dataset.GetSingleValue<string>(DicomTag.StudyDescription));
                _logger.LogInformation("Modality: {Modality}", dicomfile.Dataset.GetSingleValue<string>(DicomTag.Modality));

                // Validate dicom mendatory fields 
                if (!dataset.TryGetSingleValue<string>(DicomTag.PatientID, out _) ||
                   !dataset.TryGetSingleValue<string>(DicomTag.StudyDescription, out _) ||
                   !dataset.TryGetSingleValue<string>(DicomTag.Modality, out _))
                {
                    return new JsonResponseModel
                    {
                        Errors = true,
                        Message = "Missing essential metadata fields",
                        StatusCode = StatusCodes.Status400BadRequest
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

        [HttpPost("StoreFile")]
        public async Task<JsonResponseModel> StoreDicomFile(IFormFile file)
        {
            if (file == null || file.Length == 0) { 
                return new JsonResponseModel
                {
                    Errors = true,
                    Message = "No file uploaded.",
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            var dicomFilePath = Path.Combine(Path.GetTempPath(), file.FileName);

            using (var stream = new FileStream(dicomFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var client = new DicomClient();
                var request = new DicomCStoreRequest(dicomFilePath);
                client.AddRequest(request);

                await client.SendAsync("localhost", 104, false, "SCU", "ANY-SCP");

                return new JsonResponseModel { Errors = true, Message = "DICOM file sent successfully.", StatusCode = StatusCodes.Status400BadRequest };
            }
            catch (Exception ex)
            {
                return new JsonResponseModel { Success = true, Message = $"Error sending DICOM file: {ex.Message}", StatusCode = StatusCodes.Status400BadRequest };
            }
        }

        [HttpGet("FindFile")]
        public async Task<JsonResponseModel> FindDicomFile()
        {
            try
            {
                var client = new DicomClient();
                var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Study);
                var studies = new List<DicomDataset>();

                // Capture responses before sending the request
                request.OnResponseReceived += (req, response) =>
                {
                    if (response.Status == DicomStatus.Pending || response.Status == DicomStatus.Success)
                    {
                        studies.Add(response.Dataset);
                    }
                };

                client.AddRequest(request);

                // Send request to PACS server
                await client.SendAsync("localhost", 104, false, "SCU", "ANY-SCP");

                return new JsonResponseModel
                {
                    Success = true,
                    Message = "DICOM query completed successfully.",
                    StatusCode = StatusCodes.Status200OK,
                    Data = studies.Select(s => s.ToString()) // Convert datasets to readable format
                };
            }
            catch (Exception ex)
            {
                return new JsonResponseModel
                {
                    Success = false,
                    Message = $"Error querying DICOM server: {ex.Message}",
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

    }
}
