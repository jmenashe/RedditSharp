using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RedditSharp;
using RedditSharp.Things;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace AccountCleaner
{
    public class CommentData
    {
        public string Link, Body, BodyHtml;
    }
    public class CommentMetadata
    {
        public string Author, Subreddit;
        public int Upvotes, Downvotes;
        public CommentData Parent, Comment;
    }
    class Program
    {
        static IEnumerable<Subreddit> ReadSubreddits(string username, string password)
        {
            var reddit = new Reddit();
            var user = reddit.LogIn(username, password);
            var  subs = user.GetSubscribedSubreddits();
            return subs;
        }
        static void PruneComments(string username, string password, bool dryrun = true, int minScore = 0)
        {
            var reddit = new Reddit();
            var user = reddit.LogIn(username, password);
            var comments = user.Comments.Where(x => x.Score < minScore).ToList();
            var solutionDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            var path = Path.Combine(solutionDir, $@"deleted_comments_{DateTime.Today:yyyyMMdd}.txt");
            using (var writer = new StreamWriter(path))
            {
                writer.Write("[");
                bool first = true;
                int count = 0;
                foreach (var comment in comments)
                {
                    Console.WriteLine("Clearing comment {0} of {1}", count++, comments.Count);
                    var pieces = comment.ParentId.Split('_');
                    string split_id = pieces[0] + "_/" + string.Join("", pieces.Skip(1));
                    string parent_uri = Regex.Replace(comment.Shortlink, @"(?<=/)_/[^/]*$", split_id);
                    var parent = reddit.GetComment(new Uri(parent_uri));
                    var data = new CommentMetadata
                    {
                        Author = comment.Author,
                        Subreddit = comment.Subreddit,
                        Upvotes = comment.Upvotes,
                        Downvotes = comment.Downvotes,
                        Parent = new CommentData
                        {
                            Link = parent_uri,
                            Body = parent.Body,
                            BodyHtml = parent.BodyHtml
                        },
                        Comment = new CommentData
                        {
                            Link = comment.Shortlink,
                            Body = comment.Body,
                            BodyHtml = comment.BodyHtml
                        }
                    };
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    if (!first) writer.Write(",\n");
                    first = false;
                    writer.Write(json);
                    string format = "{0} in {1} ({2} up, {3} down)" + 
                        "\n----------------------------------\n{4}" + // parent uri
                        "\n----------------------------------\n{5}" + // parent body
                        "\n----------------------------------\n{6}" + // parent body html
                        "\n----------------------------------\n{7}" + // comment uri
                        "\n----------------------------------\n{8}" + // comment body
                        "\n----------------------------------\n{9}" + // comment body html
                        "\n----------------------------------\n\n";
                    string scomment = string.Format(format, 
                        comment.Author, comment.Subreddit, comment.Upvotes, comment.Downvotes, 
                        parent_uri, parent.Body, parent.BodyHtml, 
                        comment.Shortlink, comment.Body, comment.BodyHtml
                    );
                    writer.Write(scomment);
                    writer.Flush();
                    if (!dryrun)
                    {
                        comment.EditText("");
                        comment.Save();
                        comment.Del();
                        comment.Save();
                    }
                }
                writer.Write("]");
            }
        }
        static void Subscribe(string username, string password, IEnumerable<Subreddit> subs)
        {
            var reddit = new Reddit();
            var user = reddit.LogIn(username, password);
            foreach(var sub in subs)
            {
                sub.Subscribe();
            }
        }
        static void Main(string[] args)
        {
            PruneComments("0xjake", "omgtkkyb");
            var subs = ReadSubreddits("0xjake", "omgtkkyb");
            Subscribe("archipeepees", "AMKLPUVSUYESLNWN", subs);
        }
    }
}
