using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Assignment2.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.EntityFrameworkCore;
//using Assignment2.Models;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Assignment1.Controllers
{
    public class Home : Controller
    {
        // GET: /<controller>/
        private Assignment2DataContext _context;

        /// <param name="context"></param>
        public Home(Assignment2DataContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            User user = null;
            var jsonString = HttpContext.Session.GetString("user");
            if (jsonString != null)
            {
                user = JsonConvert.DeserializeObject<User>(jsonString);
                ViewData["user"] = user;
            }
            else
            {
                ViewData["user"] = null;
            }
            return View(_context.BlogPosts.ToList());
        }

        public IActionResult Register()
        {
            return View();
        }

        public IActionResult RegisterUser(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult VerifyUser()
        {
            var username = HttpContext.Request.Form["emailAddress"];
            var password = HttpContext.Request.Form["password"];
            var userInDatabase = (from c in _context.Users where c.EmailAddress == username select c).FirstOrDefault();

            if (userInDatabase == null)
                return RedirectToAction("Login");

            if (userInDatabase.Password != password)
                return RedirectToAction("Login");

            var userObject_JSON = JsonConvert.SerializeObject(userInDatabase);
            HttpContext.Session.SetString("user", userObject_JSON);

            return RedirectToAction("Index");
        }

        public IActionResult AddBlogPost()
        {
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;

            return View();
        }

        public IActionResult AddBlog(BlogPost blog)
        {
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;
            blog.UserId = user.UserId;

            _context.BlogPosts.Add(blog);
            _context.SaveChanges();

            BlogPost blogPost = (from c in _context.BlogPosts where c.ShortDescription == blog.ShortDescription select c).FirstOrDefault();
            HttpContext.Session.SetInt32("blogId", blogPost.BlogPostId);

            return RedirectToAction("Index");
        }

        public IActionResult EditBlogPost(int id)
        {
            HttpContext.Session.SetInt32("editBlogId", id);
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;

            var blogToEdit = (from c in _context.BlogPosts where c.BlogPostId == id select c).FirstOrDefault();
            return View(blogToEdit);
        }

        public IActionResult EditBlog(BlogPost blogPost)
        {
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;

            var id = Convert.ToInt32(Request.Form["BlogPostId"]);

            var blogToUpdate = (from c in _context.BlogPosts where c.BlogPostId == id select c).FirstOrDefault();
            blogToUpdate.Title = blogPost.Title;
            blogToUpdate.ShortDescription = blogPost.ShortDescription;
            blogToUpdate.Content = blogPost.Content;
            blogToUpdate.Posted = blogPost.Posted;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        public IActionResult DisplayFullBlogPost(int id)
        {
            User user = null;
            var jsonString = HttpContext.Session.GetString("user");
            if (jsonString != null)
            {
                user = JsonConvert.DeserializeObject<User>(jsonString);
                ViewData["RoleId"] = user.RoleId;
                ViewData["UserId"] = user.UserId;
                ViewData["user"] = user;
            }
            else
            {
                ViewData["RoleId"] = null;
                ViewData["UserId"] = null;
                ViewData["user"] = null;
            }

            var blogToDisplay = (from c in _context.BlogPosts where c.BlogPostId == id select c).FirstOrDefault();
            var articlePostedBy = (from c in _context.Users where c.UserId == blogToDisplay.UserId select c).FirstOrDefault();
            ViewData["userName"] = articlePostedBy.FirstName + " " + articlePostedBy.LastName;
            ViewData["emailAddress"] = articlePostedBy.EmailAddress;
            ViewData["comments"] = (from c in _context.Comments where c.BlogPostId == id select c);

            IQueryable<Photo> IPhotos = (from c in _context.Photos where c.BlogPostId == id select c);
            Photo[] photos = IPhotos.ToArray<Photo>();
            ViewData["photos"] = photos;

            return View(blogToDisplay);
        }

        public IActionResult AddComment(int id)
        {
            Comment comment = new Comment();
            var currentBlog = (from c in _context.BlogPosts where c.BlogPostId == id select c).FirstOrDefault();
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;

            string temp = HttpContext.Request.Form["comment"];
            temp = temp.ToLower();

            var badWords = (from c in _context.BadWords select c);
            var replacements = new Dictionary<string, string>();

            foreach (var word in badWords)
            {
                replacements.Add(word.Word, "*****");
            }

            foreach (var replacement in replacements.Keys)
            {
                temp = temp.Replace(replacement, replacements[replacement]);
            }

            comment.UserId = user.UserId;
            comment.BlogPostId = currentBlog.BlogPostId;
            comment.Content = temp;

            _context.Comments.Add(comment);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(ICollection<IFormFile> files)
        {
            var blogId = HttpContext.Session.GetInt32("blogId");
            // get your storage accounts connection string
            var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=sing0813lab5storage;AccountKey=j6u30rvHYvJkyHNfyi9bD4RjJse5Tact2iv6+Z5bb+93Y/mHbepZxfZwjqw4RbM498Mv13wPnaD6JI3psbkAsA==;EndpointSuffix=core.windows.net");

            // create an instance of the blob client
            var blobClient = storageAccount.CreateCloudBlobClient();

            // create a container to hold your blob (binary large object.. or something like that)
            // naming conventions for the curious https://msdn.microsoft.com/en-us/library/dd135715.aspx
            var container = blobClient.GetContainerReference("sing0813lab5storage");
            await container.CreateIfNotExistsAsync();

            // set the permissions of the container to 'blob' to make them public
            var permissions = new BlobContainerPermissions();
            permissions.PublicAccess = BlobContainerPublicAccessType.Blob;
            await container.SetPermissionsAsync(permissions);

            // for each file that may have been sent to the server from the client
            foreach (var file in files)
            {
                try
                {
                    // create the blob to hold the data
                    var blockBlob = container.GetBlockBlobReference(file.FileName);
                    if (await blockBlob.ExistsAsync())
                        await blockBlob.DeleteAsync();

                    using (var memoryStream = new MemoryStream())
                    {
                        // copy the file data into memory
                        await file.CopyToAsync(memoryStream);

                        // navigate back to the beginning of the memory stream
                        memoryStream.Position = 0;

                        // send the file to the cloud
                        await blockBlob.UploadFromStreamAsync(memoryStream);
                    }

                    // add the photo to the database if it uploaded successfully
                    var photo = new Photo();
                    photo.Url = blockBlob.Uri.AbsoluteUri;
                    photo.FileName = file.FileName;
                    photo.BlogPostId = (int)blogId;

                    //System.Diagnostics.Debug.WriteLine("SomeText");
                    //System.Diagnostics.Debug.WriteLine(photo.Url); 

                    _context.Photos.Add(photo);
                    _context.SaveChanges();
                }
                catch
                {
                }
            }

            int id = (int)HttpContext.Session.GetInt32("editBlogId");
            return RedirectToAction("EditBlogPost", new { id = id });
        }

        public IActionResult ViewBadWords()
        {
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;
            var badWords = (from badword in _context.BadWords select badword);

            return View(badWords);
        }

        public IActionResult DeleteBadWord(int id)
        {
            var wordToDelete = (from c in _context.BadWords where c.BadWordId == id select c).FirstOrDefault();
            _context.BadWords.Remove(wordToDelete);
            _context.SaveChanges();
            return RedirectToAction("ViewBadWords");
        }

        public IActionResult AddBadWordToDatabase()
        {
            BadWord badWordToAdd = new BadWord();
            badWordToAdd.Word = HttpContext.Request.Form["badword"];

            _context.BadWords.Add(badWordToAdd);
            _context.SaveChanges();

            return RedirectToAction("ViewBadWords");
        }

        public IActionResult EditProfile(int id)
        {
            var jsonString = HttpContext.Session.GetString("user");
            User user = JsonConvert.DeserializeObject<User>(jsonString);
            ViewData["user"] = user;

            return View(user);
        }

        public IActionResult UpdateProfile(User user)
        {
            var id = Convert.ToInt32(Request.Form["UserID"]);
            var userToUpdate = (from u in _context.Users where u.UserId == id select u).FirstOrDefault();

            userToUpdate.FirstName = user.FirstName;
            userToUpdate.LastName = user.LastName;
            userToUpdate.EmailAddress = user.EmailAddress;
            userToUpdate.Password = user.Password;
            userToUpdate.DateOfBirth = user.DateOfBirth;
            userToUpdate.Address = user.Address;
            userToUpdate.City = user.City;
            userToUpdate.Country = user.Country;
            userToUpdate.PostalCode = user.PostalCode;
            userToUpdate.RoleId = user.RoleId;

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        public IActionResult DeleteBlogPost(int id)
        {
            var blogPostToDelete = (from blogpost in _context.BlogPosts where blogpost.BlogPostId == id select blogpost).FirstOrDefault();
            var commentsAssociated = (from comment in _context.Comments where comment.BlogPostId == id select comment);
            var imagesAssociated = (from image in _context.Photos where image.PhotoId == id select image);

            _context.BlogPosts.Remove(blogPostToDelete);

            foreach (var item in commentsAssociated)
            {
                _context.Comments.Remove(item);
            }
            foreach (var image in imagesAssociated)
            {
                _context.Photos.Remove(image);
            }
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        public IActionResult LogOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }


    }
}
