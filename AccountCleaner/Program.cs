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
                        Console.WriteLine($"Clearing comment {i} of {batch.Count} (batch {batchIndex})");
                        var data = new CommentMetadata
                        {
                            Author = comment.AuthorName,
                            Subreddit = comment.Subreddit,
                            Upvotes = comment.Upvotes,
                            Downvotes = comment.Downvotes,
                            Comment = new CommentData
                            {
                                Link = comment.Shortlink,
                                Body = comment.Body,
                                BodyHtml = comment.BodyHtml
                            }
                        };
                        writer.WriteCommentData(comment, data, first);
                        if (!dryrun)
                            comment.DeleteComment();
                    }
                    if (batch.Count < batchSize)
                        break;
                    comments = comments.Skip(batchSize);
                    batchIndex++;
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
        static void WriteCommentData(this StreamWriter writer, Comment comment, CommentMetadata data, bool first)
        {
            //var parent = comment.Parent; // TODO: figure out how to get the parent
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            if (!first) writer.Write(",\n");
            writer.Write(json);
            string output = $"{comment.AuthorName} in {comment.Subreddit} ({comment.Upvotes} up, {comment.Downvotes} down)" +
                $"\n----------------------------------\n{comment.Shortlink}" +
                $"\n----------------------------------\n{comment.Body}" + 
                $"\n----------------------------------\n{comment.BodyHtml}" +
                "\n----------------------------------\n\n";
            writer.Write(output);
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
