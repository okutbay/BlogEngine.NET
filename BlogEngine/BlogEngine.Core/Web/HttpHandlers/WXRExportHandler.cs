namespace BlogEngine.Core.Web.HttpHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.Security;
    using System.Xml;

    /// <summary>
    /// Export your blog's content into WordPress eXtended RSS (WXR) file
    /// </summary>
    public class WXRExportHandler : IHttpHandler
    {
        const string dateFormatString_Default = "yyyy-MM-dd HH:mm:ss";
        const string dateFormatString_Detailed = "ddd, dd MMM yyy HH:mm:ss +0000";

        /// <summary>
        /// 
        /// </summary>
        public static List<ImageTag> Images = new List<ImageTag>();

        #region Properties

        /// <summary>
        ///     Gets a value indicating whether another request can use the <see cref = "T:System.Web.IHttpHandler"></see> instance.
        /// </summary>
        /// <value></value>
        /// <returns>true if the <see cref = "T:System.Web.IHttpHandler"></see> instance is reusable; otherwise, false.</returns>
        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region Implemented Interfaces

        #region IHttpHandler

        /// <summary>
        /// Enables processing of HTTP Web requests by a custom HttpHandler that implements the <see cref="T:System.Web.IHttpHandler"></see> interface.
        /// </summary>
        /// <param name="context">
        /// An <see cref="T:System.Web.HttpContext"></see> object that provides references to the intrinsic server 
        ///     objects (for example, Request, Response, Session, and Server) used to service HTTP requests.
        /// </param>
        public void ProcessRequest(HttpContext context)
        {
            if (Security.IsAdministrator)
            {
                context.Response.ContentType = "text/xml";
                context.Response.AppendHeader("Content-Disposition", "attachment; filename=WXR.xml");
                Images = new List<ImageTag>();
                WriteXml(context);
            }
            else
            {
                context.Response.StatusCode = 403;
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>    
        /// Take all the URLs of all pictures in the HTML.    
        /// </summary>    
        /// <param name = "Content"> HTML code </param>    
        /// <returns> Picture of URL list </returns>    
        private static List<string> GetImageListForPost(string Content)
        {
            List<string> imagesFound = new List<string>();

            if (string.IsNullOrEmpty(Content))
            {
                return imagesFound;
            }

            Regex regImg = new Regex(@"<img\b[^<>]*?\bsrc[\s\t\r\n]*=[\s\t\r\n]*[""']?[\s\t\r\n]*(?<imgUrl>[^\s\t\r\n""'<>]*)[^<>]*?/?[\s\t\r\n]*>", RegexOptions.IgnoreCase);

            // Search the matching string
            MatchCollection matches = regImg.Matches(Content);

            // acquire a list of matches    
            foreach (Match match in matches)
            {
                string item = match.Groups["imgUrl"].Value;
                imagesFound.Add(item);
            }

            return imagesFound;
        }

        /// <summary>
        /// Add categories.
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        private static void AddCategories(XmlWriter writer)
        {
            writer.WriteStartElement("categories");

            foreach (var category in Category.Categories)
            {
                writer.WriteStartElement("category");

                var parentId = "";
                if (category.Parent != null && category.Parent != Guid.Empty)
                    parentId = category.Parent.ToString();

                writer.WriteAttributeString("id", category.Id.ToString());
                writer.WriteAttributeString(
                    "date-created", category.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString(
                    "date-modified", category.DateModified.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("approved", "true");
                writer.WriteAttributeString("parentref", parentId);

                if (!String.IsNullOrEmpty(category.Description))
                {
                    writer.WriteAttributeString("description", category.Description);
                }

                writer.WriteStartElement("title");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData(category.Title);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        /// <summary>
        /// Add extended properties.
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        private static void AddExtendedProperties(XmlWriter writer)
        {
            writer.WriteStartElement("extended-properties");

            writer.WriteStartElement("property");
            writer.WriteAttributeString("name", "CommentModeration");
            writer.WriteAttributeString("value", "Anonymous");
            writer.WriteEndElement();

            writer.WriteStartElement("property");
            writer.WriteAttributeString("name", "SendTrackback");
            writer.WriteAttributeString("value", BlogSettings.Instance.EnableTrackBackSend ? "Yes" : "No");
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        /// <summary>
        /// Add post author.
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        /// <param name="post">
        /// The post to add the author on.
        /// </param>
        private static void AddPostAuthor(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("authors");
            writer.WriteStartElement("author");
            writer.WriteAttributeString("ref", post.Author);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// Add post comments.
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        /// <param name="post">
        /// The post to add comments to.
        /// </param>
        private static void AddPostComments(XmlWriter writer, Post post)
        {
            if (post.Comments.Count == 0)
            {
                return;
            }

            writer.WriteStartElement("comments");
            foreach (var comment in
                post.Comments.Where(comment => comment.Email != "trackback" && comment.Email != "pingback"))
            {
                writer.WriteStartElement("comment");
                writer.WriteAttributeString("id", comment.Id.ToString());
                writer.WriteAttributeString("parentid", comment.ParentId.ToString());
                writer.WriteAttributeString(
                    "date-created", comment.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString(
                    "date-modified", comment.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("approved", comment.IsApproved.ToString().ToLowerInvariant());
                writer.WriteAttributeString("user-name", comment.Author);
                writer.WriteAttributeString("user-email", comment.Email);
                writer.WriteAttributeString("user-ip", comment.IP);

                if (comment.Website != null)
                {
                    writer.WriteAttributeString("user-url", comment.Website.ToString());
                }
                else
                {
                    writer.WriteAttributeString("user-url", string.Empty);
                }

                writer.WriteStartElement("title");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData("re: " + post.Title);
                writer.WriteEndElement();

                writer.WriteStartElement("content");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData(comment.Content);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }



        /// <summary>
        /// Adds the post trackbacks.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add trackbacks for.</param>
        private static void AddPostTrackbacks(XmlWriter writer, Post post)
        {
            if (post.Comments.Count == 0)
            {
                return;
            }

            writer.WriteStartElement("trackbacks");
            foreach (var comment in
                post.Comments.Where(comment => comment.Email == "trackback" || comment.Email == "pingback"))
            {
                writer.WriteStartElement("trackback");
                writer.WriteAttributeString("id", comment.Id.ToString());
                writer.WriteAttributeString(
                    "date-created", comment.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString(
                    "date-modified", comment.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("approved", comment.IsApproved.ToString().ToLowerInvariant());

                if (comment.Website != null)
                {
                    writer.WriteAttributeString("url", comment.Website.ToString());
                }

                writer.WriteStartElement("title");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData(comment.Content);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }



        private static void AddPages(XmlWriter writer)
        {
            writer.WriteStartElement("posts");

            foreach (var post in Page.Pages)
            {
                writer.WriteStartElement("post");

                writer.WriteAttributeString("id", post.Id.ToString());
                writer.WriteAttributeString(
                    "date-created", post.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString(
                    "date-modified", post.DateModified.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("approved", "true");
                writer.WriteAttributeString("post-url", post.RelativeLink);
                writer.WriteAttributeString("type", "article");  // "normal" for posts and "article" for pages
                writer.WriteAttributeString(
                    "hasexcerpt", (!string.IsNullOrEmpty(post.Description)).ToString().ToLowerInvariant());
                writer.WriteAttributeString("views", "0");
                writer.WriteAttributeString("is-published", post.IsPublished.ToString());

                writer.WriteStartElement("title");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData(post.Title);
                writer.WriteEndElement();

                writer.WriteStartElement("content");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData(post.Content);
                writer.WriteEndElement();

                writer.WriteStartElement("post-name");
                writer.WriteAttributeString("type", "text");
                writer.WriteCData(post.Title);
                writer.WriteEndElement();

                writer.WriteStartElement("authors");
                writer.WriteStartElement("author");
                writer.WriteAttributeString("ref", HttpContext.Current.User.Identity.Name);
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }









        /// <summary>
        /// Adds the Image Attachments.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddImages(XmlWriter writer)
        {
            foreach (var image in Images)
            {
                writer.WriteStartElement("item");






                writer.WriteEndElement();

            }
        }







        #region Post Items





        /// <summary>
        /// Adds the post tags.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add tags to.</param>
        private static void AddPostTags(XmlWriter writer, Post post)
        {
            if (post.Tags.Count == 0)
            {
                return;
            }

            foreach (var tag in post.Tags)
            {
                writer.WriteStartElement("category");
                writer.WriteAttributeString("domain", "post_tag");
                writer.WriteAttributeString("nicename", tag);
                writer.WriteCData(tag);
                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Add post categories.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add categories to.</param>
        private static void AddPostCategories(XmlWriter writer, Post post)
        {
            if (post.Categories.Count == 0)
            {
                return;
            }

            foreach (var category in post.Categories)
            {
                writer.WriteStartElement("category");
                writer.WriteAttributeString("domain", "category");
                writer.WriteAttributeString("nicename", category.Title);
                writer.WriteCData(category.Title);
                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Adds the post is_sticky.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add a name to.</param>
        private static void AddPost_IsSticky(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "is_sticky", null);
            writer.WriteString("0");
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post_password.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add a name to.</param>
        private static void AddPost_PostPassword(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "post_password", null);
            writer.WriteCData(string.Empty);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post_type.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add a name to.</param>
        private static void AddPost_PostType(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "post_type", null);
            writer.WriteCData("post");
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post menu_order.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add a name to.</param>
        private static void AddPost_MenuOrder(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "menu_order", null);
            writer.WriteString("0");
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post_parent.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add a name to.</param>
        private static void AddPost_PostParent(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "post_parent", null);
            writer.WriteString("0");
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post status.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPost_Status(XmlWriter writer, Post post)
        {
            var status = post.IsPublished ? "publish" : "draft";

            writer.WriteStartElement("wp", "status", null);
            writer.WriteCData(status);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post name.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add a name to.</param>
        private static void AddPostName(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "post_name", null);
            writer.WriteCData(post.Slug);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post ping_status.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPost_PingStatus(XmlWriter writer, Post post)
        {
            var status = "closed";

            writer.WriteStartElement("wp", "ping_status", null);
            writer.WriteCData(status);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post comment_status.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPost_CommentStatus(XmlWriter writer, Post post)
        {
            var status = post.HasCommentsEnabled ? "open" : "closed";

            writer.WriteStartElement("wp", "comment_status", null);
            writer.WriteCData(status);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post post_date_gmt.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPost_PostDateGmt(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "post_date_gmt", null);
            writer.WriteString(post.DateCreated.ToUniversalTime().ToString(dateFormatString_Default, CultureInfo.GetCultureInfo(BlogSettings.Instance.Language)));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post post_date.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPost_PostDate(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("wp", "post_date", null);
            writer.WriteString(post.DateCreated.ToString(dateFormatString_Default, CultureInfo.GetCultureInfo(BlogSettings.Instance.Language)));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post id.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        /// <param name="Id">New generated Id</param>
        private static void AddPost_Id(XmlWriter writer, Post post, int Id)
        {
            writer.WriteStartElement("wp", "post_id", null);
            writer.WriteString(Id.ToString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post excerpt.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the excerpt to.</param>
        private static void AddPostExcerpt(XmlWriter writer, Post post)
        {
            //if (String.IsNullOrEmpty(post.Description))
            //{
            //    return;
            //}

            writer.WriteStartElement("excerpt", "encoded", null);
            writer.WriteCData(post.Description);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Add post content.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add content to.</param>
        private static void AddPostContent(XmlWriter writer, Post post)
        {
            var replacedContent = ConvertImageLinkToAbsoluteUrls(post.Content);
            writer.WriteStartElement("content", "encoded", null);
            writer.WriteCData(replacedContent);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Find and replace "src=\"/image.axd?picture=" and "href=\"/image.axd?picture=" strings to include absolute urls
        /// </summary>
        /// <param name="Content">Blog content</param>
        /// <returns>Replaced string</returns>
        private static string ConvertImageLinkToAbsoluteUrls(string Content)
        {
            //AbsoluteWebRoot ends with slash char. I choose to remove from absoluteUrl than removing from my string templates (resultImg, resultLink) for readability 
            var absoluteUrl = Utils.AbsoluteWebRoot.ToString().TrimEnd('/');
            var imgIndicator = "src=\"/image.axd?picture=";
            var linkIndicator = "href=\"/image.axd?picture=";
            var resultImg = $"src=\"{absoluteUrl}/image.axd?picture=";
            var resultLink = $"href=\"{absoluteUrl}/image.axd?picture=";
            var replacedContent = Content
                .Replace(imgIndicator, resultImg)
                .Replace(linkIndicator, resultLink);

            return replacedContent;
        }

        /// <summary>
        /// Adds the post description.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPostDescription(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("description");
            writer.WriteString(post.Description);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post guid.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        /// <param name="Id">New generated Id</param>
        private static void AddPostGuid(XmlWriter writer, Post post, int Id)
        {
            var blog_url = Utils.AbsoluteWebRoot.ToString();
            var postid = Id; //not sure is it good to override guid with integer
            var permalink = $"{blog_url}?p={postid}";
            writer.WriteStartElement("guid");
            writer.WriteAttributeString("isPermaLink", "false");
            writer.WriteString(permalink);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post creator.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPostCreator(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("dc", "creator", null);
            writer.WriteCData(post.AuthorProfile.UserName);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post pubDate.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPostPubDate(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("pubDate");
            writer.WriteString(post.DateCreated.ToString(dateFormatString_Detailed, CultureInfo.GetCultureInfo(BlogSettings.Instance.Language)));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post link.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPostLink(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("link");
            writer.WriteString(post.AbsoluteLink.ToString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post title.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        private static void AddPostTitle(XmlWriter writer, Post post)
        {
            writer.WriteStartElement("title");
            writer.WriteString(post.Title);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the post title.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="post">The post to add the title to.</param>
        /// <param name="ParentId">The post id to generate image ids</param>
        private static int ExtractImages(XmlWriter writer, Post post, int ParentId)
        {
            //get images from post content
            List<string> images = new List<string>();
            images = GetImageListForPost(post.Content);

            //populate image list
            int currentId = ParentId++;
            foreach (string image in images)
            {
                ImageTag imageTag = new ImageTag(image, DateTime.Now, post.AbsoluteLink.ToString())
                { 
                };
                Images.Add(imageTag);

                currentId++;
            }

            return currentId;
        }

        /// <summary>
        /// Adds the posts.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddPosts(XmlWriter writer)
        {
            var id = 1;

            foreach (var post in Post.Posts.OrderBy(x => x.DateCreated).ToList())
            {
                writer.WriteStartElement("item");

                //Pass the current id, add use for image ids and return last used id.
                id = ExtractImages(writer, post, id);

                AddPostTitle(writer, post);
                AddPostLink(writer, post);
                AddPostPubDate(writer, post);
                AddPostCreator(writer, post);
                AddPostGuid(writer, post, id);
                AddPostDescription(writer, post);
                AddPostContent(writer, post);
                AddPostExcerpt(writer, post);
                AddPost_Id(writer, post, id);
                AddPost_PostDate(writer, post);
                AddPost_PostDateGmt(writer, post);
                AddPost_CommentStatus(writer, post);
                AddPostName(writer, post);
                AddPost_Status(writer, post);
                AddPost_PostParent(writer, post);
                AddPost_MenuOrder(writer, post);
                AddPost_PostType(writer, post);
                AddPost_PostPassword(writer, post);
                AddPost_IsSticky(writer, post);
                AddPostCategories(writer, post);
                AddPostTags(writer, post);

                

                //writer.WriteAttributeString("id", post.Id.ToString());
                //writer.WriteAttributeString(
                //    "date-created", post.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                //writer.WriteAttributeString(
                //    "date-modified", post.DateModified.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                //writer.WriteAttributeString("approved", "true");
                //writer.WriteAttributeString("post-url", post.RelativeLink);
                //writer.WriteAttributeString("type", "normal");
                //writer.WriteAttributeString(
                //    "hasexcerpt", (!string.IsNullOrEmpty(post.Description)).ToString().ToLowerInvariant());
                //writer.WriteAttributeString("views", "0");
                //writer.WriteAttributeString("is-published", post.IsPublished.ToString());


                //
                //
                //
                //AddPostAuthor(writer, post);
                //
                //
                //AddPostComments(writer, post);
                //AddPostTrackbacks(writer, post);

                writer.WriteEndElement();

                id++;//set for next post id
            }
        }
        #endregion

        /// <summary>
        /// Add authors.
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        private static void AddAuthors(XmlWriter writer)
        {
            List<AuthorProfile> authorProfiles = AuthorProfile.Profiles;

            var id = 1;
            foreach (AuthorProfile authorProfile in authorProfiles)
            {
                writer.WriteStartElement("wp", "author", null);

                writer.WriteStartElement("wp", "author_id", null);
                writer.WriteString(id.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("wp", "author_login", null);
                writer.WriteCData(authorProfile.UserName);
                writer.WriteEndElement();

                writer.WriteStartElement("wp", "author_email", null);
                writer.WriteCData(authorProfile.EmailAddress);
                writer.WriteEndElement();

                writer.WriteStartElement("wp", "author_display_name", null);
                writer.WriteCData(authorProfile.DisplayName);
                writer.WriteEndElement();

                writer.WriteStartElement("wp", "author_first_name", null);
                writer.WriteCData(authorProfile.FirstName);
                writer.WriteEndElement();

                writer.WriteStartElement("wp", "author_last_name", null);
                writer.WriteCData(authorProfile.LastName);
                writer.WriteEndElement();

                writer.WriteEndElement();

                id++;
            }
        }

        /// <summary>
        /// Adds the wp:base_blog_url.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddBase_blog_url(XmlWriter writer)
        {
            writer.WriteStartElement("wp", "base_blog_url", null);
            writer.WriteString(Utils.AbsoluteWebRoot.ToString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the wp:base_site_url.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddBase_site_url(XmlWriter writer)
        {
            writer.WriteStartElement("wp", "base_site_url", null);
            writer.WriteString(Utils.AbsoluteWebRoot.ToString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the wp:wxr_version.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddWxr_version(XmlWriter writer)
        {
            writer.WriteStartElement("wp", "wxr_version", null);
            writer.WriteString("1.2");
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the language.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddLanguage(XmlWriter writer)
        {
            writer.WriteStartElement("language");
            writer.WriteString(BlogSettings.Instance.Language);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the pubDate.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="PubDate">Publish date</param>
        private static void AddPubDate(XmlWriter writer, DateTime PubDate)
        {
            writer.WriteStartElement("pubDate");
            writer.WriteString(PubDate.ToString(dateFormatString_Detailed, CultureInfo.GetCultureInfo(BlogSettings.Instance.Language)));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the description.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddDescription(XmlWriter writer)
        {
            writer.WriteStartElement("description");
            writer.WriteString(BlogSettings.Instance.Description);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the link.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddLink(XmlWriter writer)
        {
            writer.WriteStartElement("link");
            writer.WriteString(Utils.AbsoluteWebRoot.ToString());
            writer.WriteEndElement();
        }

        /// <summary>
        /// Adds the title.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void AddTitle(XmlWriter writer)
        {
            writer.WriteStartElement("title");
            writer.WriteString(BlogSettings.Instance.Name);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes the BlogML to the output stream.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        private static void WriteXml(HttpContext context)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                OmitXmlDeclaration = false,
                NewLineOnAttributes = false
            };

            using (var writer = XmlWriter.Create(context.Response.OutputStream, settings))
            {
                var generator = "Blog Engine for WordPress/5.3.11";
                var created = DateTime.Now;

                writer.WriteComment($" This is a WordPress eXtended RSS file generated by WordPress as an export of your site. ");
                writer.WriteComment($" It contains information about your site's posts, pages, comments, categories, and other content. ");
                writer.WriteComment($" You may use this file to transfer that content from one site to another. ");
                writer.WriteComment($" This file is not intended to serve as a complete backup of your site. ");
                writer.WriteComment($" To import this information into a WordPress site follow these steps: ");
                writer.WriteComment($" 1. Log in to that site as an administrator. ");
                writer.WriteComment($" 2. Go to Tools: Import in the WordPress admin panel. ");
                writer.WriteComment($" 3. Install the \"WordPress\" importer from the list. ");
                writer.WriteComment($" 4. Activate & Run Importer. ");
                writer.WriteComment($" 5. Upload this file using the form provided on that page. ");
                writer.WriteComment($" 6. You will first be asked to map the authors in this export file to users ");
                writer.WriteComment($"    on the site. For each author, you may choose to map to an ");
                writer.WriteComment($"    existing user on the site or to create a new user. ");
                writer.WriteComment($" 7. WordPress will then import each of the posts, pages, comments, categories, etc. ");
                writer.WriteComment($"    contained in this file into your site. ");
                writer.WriteComment($" generator=\"{generator}\" created=\"{created.ToString(dateFormatString_Default, CultureInfo.InvariantCulture)}\" ");

                writer.WriteStartElement("rss");
                //writer.WriteAttributeString("root-url", Utils.RelativeWebRoot);

                writer.WriteAttributeString("version", "2.0");
                writer.WriteAttributeString("xmlns", "excerpt", null, "http://wordpress.org/export/1.2/excerpt/");
                writer.WriteAttributeString("xmlns", "content", null, "http://purl.org/rss/1.0/modules/content/");
                writer.WriteAttributeString("xmlns", "wfw", null, "http://wellformedweb.org/CommentAPI/");
                writer.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                writer.WriteAttributeString("xmlns", "wp", null, "http://wordpress.org/export/1.2/");

                writer.WriteStartElement("channel");

                AddTitle(writer);
                AddLink(writer);
                AddDescription(writer);
                AddPubDate(writer, created);
                AddLanguage(writer);
                AddWxr_version(writer);
                AddBase_site_url(writer);
                AddBase_blog_url(writer);
                AddAuthors(writer);
                AddPosts(writer);

                //add generated images in AddPosts method
                AddImages(writer);

                //AddExtendedProperties(writer);
                //AddCategories(writer);
                //AddPages(writer);

                writer.WriteEndElement();//channel
                writer.WriteEndElement();//rss
            }
        }

        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
    public class ImageTag
    {
        /// <summary>
        /// 
        /// </summary>
        public string FullTag { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime PostDate { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string ImageLink { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string PostLink { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FullTag"></param>
        /// <param name="PostDate"></param>
        /// <param name="PostLink"></param>
        public ImageTag(string FullTag, DateTime PostDate, string PostLink)
        {
            this.FullTag = FullTag;
            this.PostDate = PostDate;
            this.PostLink = PostLink;

            setImageLink();
        }

        private void setImageLink()
        {

            this.ImageLink = string.Empty;
        }

    }
}