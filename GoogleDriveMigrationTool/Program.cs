using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveMigrationTool
{
    internal class Program
    {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/drive-dotnet-quickstart.json
        private static readonly string[] Scopes = { DriveService.Scope.Drive };
        private static readonly string ApplicationName = "Drive API .NET Quickstart";

        private static void Main()
        {
            UserCredential credential;

            using (FileStream stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Drive API service.
            DriveService driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            DrivesResource.ListRequest listRequest = driveService.Drives.List();
            DriveList driveList = listRequest.Execute();

            string sourceFolderId = GetFileId(driveService, "root", new string[] {"PC dalībniekiem" });
            Drive targetDrive = driveList.Drives.Single(d => d.Name == "PC info dalībniekiem");
            string targetFolderId = GetFileId(driveService, targetDrive.Id, new string[] { });
            //string targetFolderId = GetFileId(driveService, "root", new string[] { "PC Google My Maps" });

            CopyFiles(driveService, "/", sourceFolderId, targetFolderId, null);
        }

        private static void CopyFiles(DriveService driveService, string folderPath, string sourceFolderId, string targetFolderId, string fileName)
        {
            IEnumerable<DriveFile> sourceFiles = GetFiles(driveService, sourceFolderId, fileName);
            List<DriveFile> targetFiles = GetFiles(driveService, targetFolderId, fileName).ToList();

            foreach (DriveFile sourceFile in sourceFiles)
            {
                string filePath = folderPath + sourceFile.Name;
                Console.WriteLine($"{filePath}: {sourceFile.MimeType}");
                DriveFile targetFile = targetFiles.SingleOrDefault(f => f.Name == sourceFile.Name);
                const string folderMimeType = "application/vnd.google-apps.folder";

                //if (targetFile != null && sourceFile.MimeType != targetFile.MimeType)
                //{
                //    throw new InvalidOperationException($"Source mime type: {sourceFile.MimeType} not equal to target mime type: {targetFile.MimeType}.");
                //}

                if (sourceFile.MimeType == "application/vnd.google-apps.form")
                {
                    Console.WriteLine("Google Form detected!");
                }

                switch (sourceFile.MimeType)
                {
                    case folderMimeType:
                        if (targetFile == null)
                        {
                            Console.WriteLine("Creating folder in target...");
                            targetFile = CreateFolder(driveService, targetFolderId, sourceFile, folderMimeType);
                        }

                        CopyFiles(driveService, filePath + "/", sourceFile.Id, targetFile.Id, null);

                        //Console.WriteLine("Deleting source folder...");
                        //DeleteFile(driveService, sourceFile);
                        break;

                    case "application/vnd.google-apps.map":
                        Console.WriteLine("Google My Maps detected!");
                        break;

                    default:
                        if (targetFile != null)
                        {
                            Console.WriteLine("Deleting target file...");
                            DeleteFile(driveService, targetFile);
                        }

                        Console.WriteLine("Copying source file to target...");
                        CopyFile(driveService, sourceFile, targetFolderId);

                        //Console.WriteLine("Deleting source file...");
                        //DeleteFile(driveService, sourceFile);
                        break;
                }
            }
        }

        private static DriveFile CreateFolder(DriveService driveService, string targetFolderId, DriveFile sourceFile, string folderMimeType)
        {
            DriveFile targetFile = new DriveFile
            {
                MimeType = folderMimeType,
                Name = sourceFile.Name,
                Parents = new[] { targetFolderId },
            };
            FilesResource.CreateRequest createRequest = driveService.Files.Create(targetFile);
            createRequest.SupportsAllDrives = true;
            targetFile = createRequest.Execute();
            return targetFile;
        }

        private static void DeleteFile(DriveService driveService, DriveFile targetFile)
        {
            FilesResource.DeleteRequest deleteRequest = driveService.Files.Delete(targetFile.Id);
            deleteRequest.SupportsAllDrives = true;
            _ = deleteRequest.Execute();
        }

        private static void CopyFile(DriveService driveService, DriveFile sourceFile, string parentFolderId)
        {
            DriveFile fileCopy = new DriveFile
            {
                MimeType = sourceFile.MimeType,
                Name = sourceFile.Name,
                Parents = new[] { parentFolderId },
            };
            FilesResource.CopyRequest copyRequest = driveService.Files.Copy(fileCopy, sourceFile.Id);
            copyRequest.SupportsAllDrives = true;
            _ = copyRequest.Execute();
        }

        private static IEnumerable<DriveFile> GetFiles(DriveService driveService, string folderId, string fileName)
        {
            // Define parameters of request.
            FilesResource.ListRequest listRequest = driveService.Files.List();
            listRequest.Fields = "*";
            listRequest.IncludeItemsFromAllDrives = true;
            listRequest.OrderBy = "name";
            listRequest.SupportsAllDrives = true;
            listRequest.Q = $"'{folderId}' in parents and not trashed";

            if (fileName != null)
            {
                listRequest.Q += $" and name = '{QuoteFileName(fileName)}'";
            }

            //listRequest.PageSize = 10;
            //listRequest.Fields = "nextPageToken, files(id, name)";

            do
            {
                // List files.
                FileList fileList = listRequest.Execute();

                foreach (DriveFile file in fileList.Files)
                {
                    yield return file;
                }
            } while (listRequest.PageToken != null);
        }

        private static string GetFileId(DriveService driveService, string folderId, IEnumerable<string> pathFromFolder)
        {
            string fileId = folderId;

            foreach (string pathComponent in pathFromFolder)
            {
                List<DriveFile> files = GetFiles(driveService, fileId, pathComponent).ToList();
                DriveFile file = files.Single();
                fileId = file.Id;
            }

            return fileId;
        }

        private static string QuoteFileName(string fileName)
        {
            return fileName.Replace("'", @"\'");
        }
    }
}
