﻿using DAL.DataModels;
using HalloDoc_Project.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using DAL.DataContext;
using DAL.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Web;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using System.Net.Cache;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Net.Mail;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BAL.Interfaces;
using BAL.Repository;
namespace HalloDoc_Project.Controllers
{
    [CustomAuthorize("Patient")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;
        private readonly IRequestRepo _patient_Request;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IFileOperations _insertfiles;
        private readonly IPatientDashboard _patientDashboard;
        public HomeController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration config, IRequestRepo request, IPasswordHasher passwordHasher,IFileOperations insertfiles, IPatientDashboard patientDashboard)
        {
            _context = context;
            _environment = environment;
            _config = config;
            _patient_Request = request;
            _passwordHasher = passwordHasher;
            _insertfiles = insertfiles;
            _patientDashboard = patientDashboard;
        }
        //DownloadAllFiles, Logout and error method are not converted to three tier.
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult patient_submit_request_screen()
        {
            return View();
        }
        public IActionResult SelectedDownload()
        {
            return View();
        }
        
        [HttpPost]
        public JsonResult CheckEmail(string email)
        {
            bool emailExists = _context.Users.Any(u => u.Email == email);
            return Json(new { exists = emailExists });
        }
        public IActionResult PatientProfile()
        {
            var email = HttpContext.Session.GetString("Email");
            PatientProfileViewModel ppm= _patientDashboard.PatientProfile(email);
            return View(ppm);
        }
        public IActionResult forgot_password_page()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult editprofile(PatientProfileViewModel ppm)
        {
            //string phoneNumber = "+" + /*pm.CountryCode*/ + '-' + pm.Phone;
            var email=HttpContext.Session.GetString("Email");
            _patientDashboard.EditProfile(ppm, email);
            
            return RedirectToAction("PatientProfile");
        }
        public IActionResult ViewDocuments(int requestid)
        {
            string? email = HttpContext.Session.GetString("Email");
            ViewDocumentsViewModel vm = _patientDashboard.ViewPatientDocsGet(requestid,email);
            return View(vm);
        }
        [HttpPost]
        public IActionResult ViewDocuments(ViewDocumentsViewModel vm)
        {
            string path = _environment.WebRootPath;
            vm = _patientDashboard.ViewPatientDocsPost(vm, path);
            return ViewDocuments(vm.RequestID);
        }
        public IActionResult PatientDashboard()
        {
            var email = HttpContext.Session.GetString("Email");
            PatientDashboardViewModel pd= _patientDashboard.PatientDashboard(email);

            return View(pd);
        }
        public async Task<IActionResult> DownloadAllFiles(int requestId)
        {
            try
            {
                // Fetch all document details for the given request:
                var documentDetails = _context.Requestwisefiles.Where(m => m.Requestid == requestId).ToList();

                if (documentDetails == null || documentDetails.Count == 0)
                {
                    return NotFound("No documents found for download");
                }

                // Create a unique zip file name
                var zipFileName = $"Documents_{DateTime.Now:yyyyMMddHHmmss}.zip";
                var zipFilePath = Path.Combine(_environment.WebRootPath, "DownloadableZips", zipFileName);

                // Create the directory if it doesn't exist
                var zipDirectory = Path.GetDirectoryName(zipFilePath);
                if (!Directory.Exists(zipDirectory))
                {
                    Directory.CreateDirectory(zipDirectory);
                }

                // Create a new zip archive
                using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    // Add each document to the zip archive
                    foreach (var document in documentDetails)
                    {
                        var documentPath = Path.Combine(_environment.WebRootPath, "Content", document.Filename);
                        zipArchive.CreateEntryFromFile(documentPath, document.Filename);
                    }
                }

                // Return the zip file for download
                var zipFileBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
                return File(zipFileBytes, "application/zip", zipFileName);
            }
            catch
            {
                return BadRequest("Error downloading files");
            }
        }

        public IActionResult CreateNewRequestForMe()
        {
            int userId = (int)HttpContext.Session.GetInt32("userId");
            User user = _context.Users.FirstOrDefault(u => u.Userid == userId);
            var region = _context.Regions.ToList();

            PatientRequestForMe createRequestForMe = new PatientRequestForMe()
            {
                UserName = user.Firstname + " " + user.Lastname,
                FirstName = user.Firstname,
                LastName = user.Lastname,
                //DOB = user
                PhoneNumber = user.Mobile,
                Email = user.Email,
                Street = user.Street,
                City = user.City,
                State = user.Regionid,
                Zipcode = user.Zipcode,
                regions = region,
            };
            return View(createRequestForMe);
        }

        [HttpPost]
        public IActionResult CreateNewRequestForMe(PatientRequestForMe pi)
        {
            var region = _context.Regions.FirstOrDefault(x => x.Regionid == pi.State);
            string phone = "+" + pi.code + "-" + pi.PhoneNumber;

            var UserEmail=HttpContext.Session.GetString("Email");
            User user = _context.Users.FirstOrDefault(u => u.Email == UserEmail);

            var request = new Request
            {
                Requesttypeid = 2,
                Userid = user.Userid,
                Firstname = pi.FirstName,
                Lastname = pi.LastName,
                Phonenumber = phone,
                Email = pi.Email,
                Status = 1,
                Createddate = DateTime.Now,
                Isdeleted = false,
                Confirmationnumber = _passwordHasher.GenerateConfirmationNumber(user)
            };
            _context.Requests.Add(request);
            _context.SaveChanges();


            var requestClient = new Requestclient
            {
                Requestid = request.Requestid,
                Firstname = pi.FirstName,
                Lastname = pi.LastName,
                Phonenumber = phone,
                Street = pi.Street,
                City = pi.City,
                Regionid = pi.State,
                State = region.Name,
                Zipcode = pi.Zipcode,
                Email = pi.Email,
                Location = pi.City + " " + pi.State,
                Address = pi.Street + ", " + pi.City + ", " + pi.State + " - " + pi.Zipcode,
                Strmonth = pi.DOB.Value.Month.ToString(),
                Intdate = pi.DOB.Value.Day,
                Intyear = pi.DOB.Value.Year,
                Notes = pi.symptoms
            };
            _context.Requestclients.Add(requestClient);
            _context.SaveChanges();


            if (pi.File != null)
            {
                Guid myuuid = Guid.NewGuid();
                var filename = Path.GetFileName(pi.File.FileName);
                //var FinalFileName = myuuid.ToString() + filename;
                var FinalFileName = $"{myuuid.ToString()}${filename}";

                //path

                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "uploads", FinalFileName);

                //copy in stream

                using (var str = new FileStream(filepath, FileMode.Create))
                {
                    //copy file
                    pi.File.CopyTo(str);
                }

                //STORE DATA IN TABLE
                var fileupload = new Requestwisefile()
                {

                    Requestid = request.Requestid,
                    Filename = FinalFileName,
                    Createddate = DateTime.Now,
                };

                _context.Requestwisefiles.Add(fileupload);
                _context.SaveChanges();
            }


            return RedirectToAction("patientDashboard");
        }

        public IActionResult CreateNewRequestForSomeone()
        {
            int? userId = (int)HttpContext.Session.GetInt32("userId");
            //User user = _context.Users.FirstOrDefault(u => u.Userid == userId);
            var regions = _context.Regions.ToList();

            PatientRequestForSomeone patientRequestSomeoneModel = new PatientRequestForSomeone()
            {
                //UserName = user.Firstname + " " + user.Lastname,
                regions = regions
            };
            return View(patientRequestSomeoneModel);
        }

        [HttpPost]
        public IActionResult CreateNewRequestForSomeone(PatientRequestForSomeone patientInfoBySome)
        {
            var region = _context.Regions.FirstOrDefault(x => x.Regionid == patientInfoBySome.State);

            string phone = "+" + patientInfoBySome.code + "-" + patientInfoBySome.PhoneNumber;

            //int? userId = (int)HttpContext.Session.GetInt32("userId");

            bool isUserExists = _context.Users.Any(u => u.Email == patientInfoBySome.Email);

            if (isUserExists)
            {
                User user = _context.Users.FirstOrDefault(u => u.Email == patientInfoBySome.Email);
                var request = new Request
                {
                    Requesttypeid = 3,
                    Userid = user.Userid,
                    Firstname = patientInfoBySome.FirstName,
                    Lastname = patientInfoBySome.LastName,
                    Phonenumber = phone,
                    Email = patientInfoBySome.Email,
                    Relationname = patientInfoBySome.Relation,
                    Status = 1,
                    Createddate = DateTime.Now,
                    Isdeleted = false,
                    Confirmationnumber = _passwordHasher.GenerateConfirmationNumber(user),
                };
                _context.Requests.Add(request);
                _context.SaveChanges();


                var requestClient = new Requestclient
                {
                    Requestid = request.Requestid,
                    Firstname = patientInfoBySome.FirstName,
                    Lastname = patientInfoBySome.LastName,
                    Phonenumber = phone,
                    Email = patientInfoBySome.Email,
                    Street = patientInfoBySome.Street,
                    City = patientInfoBySome.City,
                    Regionid = patientInfoBySome.State,
                    State = region.Name,
                    Zipcode = patientInfoBySome.Zipcode,
                    Intdate = patientInfoBySome.DOB.Value.Day,
                    Strmonth = patientInfoBySome.DOB.Value.Month.ToString(),
                    Intyear = patientInfoBySome.DOB.Value.Year,
                    Notes = patientInfoBySome.symptoms,
                    Address = patientInfoBySome.Street + ", " + patientInfoBySome.City + ", " + patientInfoBySome.State + " - " + patientInfoBySome.Zipcode,
                };
                _context.Requestclients.Add(requestClient);
                _context.SaveChanges();



                if (patientInfoBySome.File != null)
                {
                    Guid myuuid = Guid.NewGuid();
                    var filename = Path.GetFileName(patientInfoBySome.File.FileName);
                    //var FinalFileName = myuuid.ToString() + filename;
                    var FinalFileName = $"{myuuid.ToString()}${filename}";

                    //path

                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "uploads", FinalFileName);

                    //copy in stream

                    using (var str = new FileStream(filepath, FileMode.Create))
                    {
                        //copy file
                        patientInfoBySome.File.CopyTo(str);
                    }

                    //STORE DATA IN TABLE
                    var fileupload = new Requestwisefile()
                    {

                        Requestid = request.Requestid,
                        Filename = FinalFileName,
                        Createddate = DateTime.Now,
                    };

                    _context.Requestwisefiles.Add(fileupload);
                    _context.SaveChanges();
                }


            }
            else
            {
                var newaspNetUser = new Aspnetuser()
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = patientInfoBySome.Email,
                    Email = patientInfoBySome.Email,
                    Createddate = DateTime.Now,
                    Phonenumber = phone,
                };
                _context.Aspnetusers.Add(newaspNetUser);
                _context.SaveChanges();

                var user = new User()
                {
                    Aspnetuserid = newaspNetUser.Id,
                    Firstname = patientInfoBySome.FirstName,
                    Lastname = patientInfoBySome.LastName,
                    Email = patientInfoBySome.Email,
                    Mobile = phone,
                    Createdby = newaspNetUser.Id,
                    Createddate = DateTime.Now,
                    Strmonth = patientInfoBySome.DOB.Value.Month.ToString(),
                    Intdate = patientInfoBySome.DOB.Value.Day,
                    Intyear = patientInfoBySome.DOB.Value.Year,
                    Street = patientInfoBySome.Street,
                    City = patientInfoBySome.City,
                    Regionid = patientInfoBySome.State,
                    State = region.Name,
                    Zipcode = patientInfoBySome.Zipcode,
                };
                _context.Users.Add(user);
                _context.SaveChanges();

                var request = new Request
                {
                    Requesttypeid = 3,
                    Userid = user.Userid,
                    Firstname = patientInfoBySome.FirstName,
                    Lastname = patientInfoBySome.LastName,
                    Phonenumber = phone,
                    Email = patientInfoBySome.Email,
                    Relationname = patientInfoBySome.Relation,
                    Status = 1,
                    Createddate = DateTime.Now,
                    Isdeleted = false,
                    Confirmationnumber = _passwordHasher.GenerateConfirmationNumber(user),
                };
                _context.Requests.Add(request);
                _context.SaveChanges();


                var requestClient = new Requestclient
                {
                    Requestid = request.Requestid,
                    Firstname = patientInfoBySome.FirstName,
                    Lastname = patientInfoBySome.LastName,
                    Phonenumber = phone,
                    Email = patientInfoBySome.Email,
                    Street = patientInfoBySome.Street,
                    City = patientInfoBySome.City,
                    Regionid = patientInfoBySome.State,
                    State = region.Name,
                    Zipcode = patientInfoBySome.Zipcode,
                    Intdate = patientInfoBySome.DOB.Value.Day,
                    Strmonth = patientInfoBySome.DOB.Value.Month.ToString(),
                    Intyear = patientInfoBySome.DOB.Value.Year,
                    Notes = patientInfoBySome.symptoms,
                    Address = patientInfoBySome.Street + ", " + patientInfoBySome.City + ", " + patientInfoBySome.State + " - " + patientInfoBySome.Zipcode,
                };
                _context.Requestclients.Add(requestClient);
                _context.SaveChanges();



                if (patientInfoBySome.File != null)
                {
                    Guid myuuid = Guid.NewGuid();
                    var filename = Path.GetFileName(patientInfoBySome.File.FileName);
                    //var FinalFileName = myuuid.ToString() + filename;
                    var FinalFileName = $"{myuuid.ToString()}${filename}";

                    //path

                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "uploads", FinalFileName);

                    //copy in stream

                    using (var str = new FileStream(filepath, FileMode.Create))
                    {
                        //copy file
                        patientInfoBySome.File.CopyTo(str);
                    }

                    //STORE DATA IN TABLE
                    var fileupload = new Requestwisefile()
                    {
                        Requestid = request.Requestid,
                        Filename = FinalFileName,
                        Createddate = DateTime.Now,
                    };

                    _context.Requestwisefiles.Add(fileupload);
                    _context.SaveChanges();
                }
            }
            return RedirectToAction("patientDashboard");
        }

        public IActionResult logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("login_page", "Guest");
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}