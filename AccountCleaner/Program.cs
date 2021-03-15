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
    public class Credentials
    {
        public string Username, Password;
    }
    static class Program
    {
        static void PruneComments(this Reddit reddit, bool dryrun = true, int minScore = 0)
        {
            var comments = reddit.User.Comments.AsQueryable().Where(x => x.Score < minScore);
            var solutionDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            var path = Path.Combine(solutionDir, $@"deleted_comments_{DateTime.Today:yyyyMMdd}.txt");
            int batchSize = 10;
            int batchIndex = 0;
            using (var writer = new StreamWriter(path))
            {
                writer.Write("[");
                bool first = true;
                while (true)
                {
                    var batch = comments.Take(batchSize).ToList();
                    for(int i = 0; i < batch.Count; i++)
                    {
                        var comment = batch[i];
                        Console.WriteLine($"Clearing comment {i} of {batch.Count} (batch {batchIndex++})");
                        var pieces = comment.ParentId.Split('_');
                        string split_id = pieces[0] + "_/" + string.Join("", pieces.Skip(1));
                        string parent_uri = Regex.Replace(comment.Shortlink, @"(?<=/)_/[^/]*$", split_id);
                        var parent = reddit.GetComment(new Uri(parent_uri));
                        var data = new CommentMetadata
                        {
                            Author = comment.AuthorName,
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
                        writer.WriteCommentData(comment, parent, data, first);
                        if (!dryrun)
                            comment.DeleteComment();
                    }
                    if (batch.Count < batchSize)
                        break;
                    else
                        comments = comments.Skip(batchSize);
                    first = false;
                }
                writer.Write("]");
            }
        }

        static void DeleteComment(this Comment comment)
        {
            comment.EditText("");
            comment.Save();
            comment.Del();
            comment.Save();
        }
        static void WriteCommentData(this StreamWriter writer, Comment comment, Comment parent, CommentMetadata data, bool first)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            if (!first) writer.Write(",\n");
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
                comment.AuthorName, comment.Subreddit, comment.Upvotes, comment.Downvotes,
                parent.Permalink, parent.Body, parent.BodyHtml,
                comment.Shortlink, comment.Body, comment.BodyHtml
            );
            writer.Write(scomment);
            writer.Flush();
        }
        static void CopySubscriptions(this Reddit target, Reddit source)
        {
            foreach(var sourceSub in source.User.SubscribedSubreddits)
            {
                var targetSub = target.GetSubreddit(sourceSub.Name);
                targetSub.Subscribe();
            }
        }
        static void Main(string[] args)
        {
            var source = new Reddit();
            source.LogIn("0xjake", "9jKJPZUc9VVnh2hwKK6kaCBs");
            var target = new Reddit();
            target.LogIn("archipeepees", "UUFLPPPAFUCWYZMH");

            //target.CopySubscriptions(source);
            source.PruneComments();
        }
    }
}
