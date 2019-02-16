﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Deployer.Execution;

namespace Deployer.Raspberry.Tasks
{
    [TaskDescription("Downloading UEFI")]
    public class UefiDownload : IDeploymentTask
    {
        private readonly IGitHubClient githubCient;
        private readonly IFileSystemOperations fileSystemOperations;
        private const string DownloadFolder = @"Downloaded\UEFI";

        public UefiDownload(IGitHubClient githubCient, IFileSystemOperations fileSystemOperations)
        {
            this.githubCient = githubCient;
            this.fileSystemOperations = fileSystemOperations;
        }

        public async Task Execute()
        {
            if (fileSystemOperations.DirectoryExists(DownloadFolder))
            {
                return;
            }

            using (var stream = await githubCient.Open("https://github.com/andreiw/RaspberryPiPkg"))
            {
                var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

                var mostRecentFolderEntry = GetMostRecentDirEntry(zipArchive);

                var contents = zipArchive.Entries.Where(x => x.FullName.StartsWith(mostRecentFolderEntry.FullName) && !x.FullName.EndsWith("/"));
                await ExtractContents(DownloadFolder, mostRecentFolderEntry, contents);
            }
        }

        private async Task ExtractContents(string destination, ZipArchiveEntry baseEntry,
            IEnumerable<ZipArchiveEntry> entries)
        {
            foreach (var entry in entries)
            {
                var filePath = entry.FullName.Substring(baseEntry.FullName.Length);

                var destFile = Path.Combine(destination, filePath.Replace("/", "\\"));
                var dir = Path.GetDirectoryName(destFile);
                if (!fileSystemOperations.DirectoryExists(dir))
                {
                    fileSystemOperations.CreateDirectory(dir);
                }

                using (var destStream = File.Open(destFile, FileMode.OpenOrCreate))
                using (var stream = entry.Open())
                {
                    await stream.CopyToAsync(destStream);
                }
            }
        }

        private ZipArchiveEntry GetMostRecentDirEntry(ZipArchive p)
        {
            var dirs = from e in p.Entries
                where e.FullName.EndsWith("/")
                select e;

            var splitted = from e in dirs
                select new
                {
                    e,
                    Parts = e.FullName.Split('/'),
                };

            var parsed = from r in splitted
                select new
                {
                    r.e,
                    Date = FirstParseableOrNull(r.Parts),
                };

            return parsed.OrderByDescending(x => x.Date).First().e;
        }

        private DateTime? FirstParseableOrNull(string[] parts)
        {
            foreach (var part in parts)
            {
                var candidate = part.Split('-');
                if (candidate.Length > 1)
                {
                    var datePart = candidate[0];
                    if (DateTime.TryParseExact(datePart, "yyyyMMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        return date;
                    }
                }
            }

            return null;
        }
    }
}