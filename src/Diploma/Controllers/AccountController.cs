﻿using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Mvc;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Diploma.Models;
using Diploma.ViewModels;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Server;
using Microsoft.AspNet.Http;
using System.Net.Mail;
using System.Net;

namespace Diploma.Controllers
{
    public class AccountController : Controller
    {
        public const string WebSite = "http://192.168.68.82:5000";
        private DashboardContext _context;
        private SignInManager<User> _signInManager;
        private UserManager<User> _userManager;

        public AccountController(SignInManager<User> signInManager,UserManager<User> userManager, DashboardContext context)
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if(User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Home", "Index");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel vm,string returnUrl)
        {
            if(ModelState.IsValid)
            {
                var signInResult = await _signInManager.PasswordSignInAsync(vm.Username,
                                                                      vm.Password,
                                                                      true, false);
                if (signInResult.Succeeded)
                {
                    if(string.IsNullOrWhiteSpace(returnUrl))
                    {
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        return Redirect(returnUrl);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Username or Password incorrecct!");
                }
            }
            
            return View();
        }

        [Authorize]
        public async Task<ActionResult> Logout()
        {
            if (User.Identity.IsAuthenticated)
            {
                await _signInManager.SignOutAsync();
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Registration()
        {
            
            var Subjects = _context.Subjects.ToList();
            List<string> ViewSubj = new List<string>();
            foreach (var Sub in Subjects)
            {
                    ViewSubj.Add(Sub.Name);
            }
            ViewBag.Subjects = ViewSubj;

            var Roles = _context.Roles.ToList().OrderBy(t=>t.Name);
            List<string> ViewRoles = new List<string>();
            foreach (var Role in Roles)
            {
                if (Role.Name != "Admin")
                    ViewRoles.Add(Role.Name);
            }
            ViewBag.Roles = ViewRoles;


            var Groups = _context.Groups.ToList();
            List<string> ViewGroups = new List<string>();
            foreach (var Group in Groups)
            {
                    ViewGroups.Add(Group.GroupName);
            }
            ViewBag.Groups = ViewGroups;
            
            return View();
        }

        //needs to be checked
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registration(RegistrationViewModel RegisteredUser)
        {
            var user = new User()
            {
                UserName = RegisteredUser.Username,
                Email = RegisteredUser.Email,
                Name = RegisteredUser.Name,
                Surname = RegisteredUser.Surname,
                CreateDate = System.DateTime.Now,
            };

            if (RegisteredUser.Role == "Student")
            {
                var group = _context.Groups.FirstOrDefault(t => t.GroupName == RegisteredUser.Group);
                if (group != null)
                {
                    var Student = new Student()
                    {
                        StudentCard = RegisteredUser.StudentCard,
                        Group = group,
                        GroupId = group.GroupId,
                        User = user,
                        UserId = user.Id
                    };
                    user.Student = Student;
                    _context.Students.Add(Student);
                }
                
            }
            else
            {
                var Teacher = new Teacher()
                {
                    UserId = user.Id
                };
                user.Teacher = Teacher;
                _context.Teachers.Add(Teacher);
                foreach (var g in RegisteredUser.Groups)
                {
                    var group = _context.Groups.FirstOrDefault(t => t.GroupName == g);
                    if (group != null)
                    {
                        var T = new TeacherGroup()
                        {
                            Group = group,
                            GroupId = group.GroupId,
                            Teacher = Teacher,
                            TeacherId = Teacher.TeacherId
                        };
                        _context.TeacherGroup.Add(T);
                    }
                }
                foreach (var sub in RegisteredUser.Subjects)
                {
                    var subj = _context.Subjects.FirstOrDefault(t => t.Name == sub);
                    if (subj != null)
                    {
                        var T = new SubjectTeacher()
                        {
                            Subject = subj,
                            SubjectId = subj.SubjectId,
                            Teacher = Teacher,
                            TeacherId = Teacher.TeacherId
                        };
                        _context.SubjectTeacher.Add(T);
                    }
                }
            }

            if (RegisteredUser.Avatar != null)
            {
                string nameForAvatar = user.Id + '.' + GetFileName(RegisteredUser.Avatar.ContentDisposition)[1];
                string pathForAvatar = "wwwroot/Images/" + nameForAvatar;
                RegisteredUser.Avatar.SaveAs(pathForAvatar);
                user.Avatar = nameForAvatar.ToString();
            }

            var success = await _userManager.CreateAsync(user, RegisteredUser.Password);
            var result = await _userManager.AddToRoleAsync(user, RegisteredUser.Role);
            if (success.Succeeded && result.Succeeded)
            {
                SendConfirmationEmail(user);
                _context.SaveChanges();
                return RedirectToAction("Index", "Home");
            }
            return View(RegisteredUser);
        }



        public async void SendConfirmationEmail(User user)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action(
                "ConfirmEmail",
                "Account",
                new
                {
                    UserId = user.Id,
                    code = code
                }
                );
            string message = "Please confirm your account by clicking this link: <a href=\"http://" + WebSite + callbackUrl + "\">link</a>";
            string subject = "Confirm your email";

            SendEmailAsync(message, subject, user.Email);
        }

        public async Task<ActionResult> ConfirmEmail(string UserId,string code)
        {
            if (UserId == null || code == null)
            {
                return View("~/Views/Error/Error");
            }
            var user = _context.Users.First(c => c.Id == UserId);
            var result = await _userManager.ConfirmEmailAsync(user, code);
            if(result.Succeeded)
            {
                return View();
            }
            ViewBag.Email = user.Email;
            return View("~/Views/Error/Error");
        }

        static async void SendEmailAsync(string message, string subject,string usermail)
        {
            if(message != "" && subject != "" && usermail != "")
            await Task.Run(() =>
            {
                string AppMail = "test.acc.socnet@gmail.com";
                string Password = "QSCazx321";
                var mail = new MailMessage(
                    AppMail,
                    usermail
                    );
                mail.Body = message;
                mail.Subject = subject;
                mail.IsBodyHtml = true;               
                SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 25);
                smtpClient.Credentials = new NetworkCredential(
                    AppMail, 
                    Password
                    );
                smtpClient.EnableSsl = true;
                smtpClient.Send(mail);
            });
        }

        internal static string[] GetFileName(string init)
        {
            string filename_full = init.Substring(init.IndexOf("filename=\""));
            return filename_full.Substring(10, filename_full.Length - 11).Split('.');
        }


        //change according to teacher and student
        
        [Authorize]
        public async Task<IActionResult> ChangeProfile()
        {
            User currentUser = await _userManager.FindByNameAsync(User.Identity.Name);
            ChangeProfileViewModel ChangeProfile = new ChangeProfileViewModel()
            {
                Username = currentUser.UserName,
                Name = currentUser.Name,
                Surname = currentUser.Surname,
                Email = currentUser.Email,
                EmailConfirmed = currentUser.EmailConfirmed 
            };
            return View(ChangeProfile);
        }
         
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeProfile(ChangeProfileViewModel _changeProfile)
        {
            User currentUser = await _userManager.FindByNameAsync(User.Identity.Name);
            
            currentUser.Name = _changeProfile.Name;
            currentUser.Surname = _changeProfile.Surname;
            currentUser.Email = _changeProfile.Email;
            currentUser.UserName = _changeProfile.Username;
            await _userManager.UpdateAsync(currentUser);
            _context.SaveChanges();
            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(currentUser, true);
            return RedirectToAction("Index", "Home");
        }
    }
}