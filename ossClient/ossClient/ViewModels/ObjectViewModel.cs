﻿using OssClientMetro.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using Oss;
using OssClientMetro.Events;
using OssClientMetro.Model;
using System.IO;
using OssClientMetro.Services;
using System.IO.Compression;

namespace OssClientMetro.ViewModels
{
    class ObjectViewModel : PropertyChangedBase, IRightWorkSpace, IHandle<BuketSelectedEvent>, IHandle<CreateFolderEvent>
    {
        readonly IEventAggregator events;
        readonly IClientService clientService;
        readonly IWindowManager windowManager;
        readonly IFileFolderDialogService fileFolderDialogService;

        public ObjectViewModel(IEventAggregator _events, IClientService _clientService,
            IWindowManager _windowManager, IFileFolderDialogService _fileFolderDialogServic)
       {
            this.events = _events;
            clientService = _clientService;
            windowManager = _windowManager;
            fileFolderDialogService = _fileFolderDialogServic;
            events.Subscribe(this);
            objectList = new BindableCollection<ObjectModel>();
            folderListModel = _clientService.folders;
            history = new History();

        }


       private int m_selectedIndex = 0;

       public int selectedIndex
       {
           get
           {
               return this.m_selectedIndex;
           }
           set
           {
               this.m_selectedIndex = value;
               NotifyOfPropertyChange(() => this.selectedIndex);
           }
       }

       CreateFolderViewModel createFolderVM;

       public void createFolderOperate()
       {
           createFolderVM = new CreateFolderViewModel(events);
           windowManager.ShowWindow(createFolderVM);
       }

       private async Task createFolder(string bucketName, string key)  //key is the parent 
       {
           MemoryStream s = new MemoryStream();
           ObjectMetadata oMetaData = new ObjectMetadata();
           OssObjectSummary ossObjSummary = new OssObjectSummary();
           ossObjSummary.BucketName = bucketName;

           ossObjSummary.Key = key;

           await clientService.ossClient.PutObject(ossObjSummary.BucketName, ossObjSummary.Key, s, oMetaData);
           s.Dispose();
       }

        public void refreshObjectList(FolderModel folderModel)
        {
            if (folderModel == null)
            {
                objectList.Clear();
            }
            else
            {
                objectList.Clear();
                objectList.AddRange(folderModel.objList);
                objectList.AddRange(folderModel.folderList);
            }

        }

        public async void OpenFolder()
       {
           ObjectModel temp = objectList[selectedIndex];

           if (temp is FolderModel)
           {
               currentFolder = await folderListModel.getFolderModel(temp.bucketName, temp.key);
               refreshObjectList(currentFolder);
               history.add(temp.bucketName + "/" + temp.key);
           }
       }

        public async void refresh()
        {
            currentFolder = await folderListModel.refreshFolderModel(currentFolder.bucketName, currentFolder.key);
            refreshObjectList(currentFolder);
        }

    

         public async   void Handle(BuketSelectedEvent message)
         {
             if (message.BuketName != null)
             {
                 currentFolder = await folderListModel.getFolderModel(message.BuketName);
                 refreshObjectList(currentFolder);
                 history.add(message.BuketName + "/");
             }
             else
             {
                 currentFolder = null;
                 refreshObjectList(null);
             }
         }

         public async void Handle(CreateFolderEvent message)
         {
             try
             { 
                  await createFolder(currentFolder.bucketName, currentFolder.key + message.folderName + "/");
                  refresh();

             }
             catch (Exception ex)
             {

             }
         }

         public async void delete()
         {
             if (selectedIndex < 0)
             {


             }
             else
             {
                ObjectModel objModel = objectList[selectedIndex];
                if (objModel is FileModel)
                {
                    await folderListModel.deleteFile(objModel.bucketName, objModel.key);
                }
                else
                {
                    await folderListModel.deleteFolder(objModel.bucketName, objModel.key);
                }
              
                 refresh();
             }
         }


       public  void goback()
         {
            history.goBack();
            refreshPath();           
         }

       async Task refreshPath()
       {
           string[] ss = history.NowPath.Split('/');
           if (currentFolder.bucketName != ss[0])
           {
               events.Publish(new BuketSelectedUiUpdateEvent(ss[0]));
           }
           currentFolder = await folderListModel.getFolderModel(ss[0], history.NowPath.Substring(ss[0].Length + 1));
           refreshObjectList(currentFolder);
       }

       public void gofoward()
         {

             history.goForward();
             refreshPath();

         }


       async Task downloadfile(FileModel fileModel, string fileName)
       {
           OssObject obj = await folderListModel.downloadFile(fileModel.bucketName, fileModel.key, fileModel.callback);
           Stream stream = obj.Content;
           FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate);
           await stream.CopyToAsync(fs);
           fs.Position = 0;
           fs.Flush();
           fs.Close();
           stream.Close();
       }

       async Task downloadFolder(FolderModel folderModel, string savePath)
       {
           List<Task> taskList = new List<Task>();
           foreach (FileModel file in folderModel.objListAll)
           {
               string fileName = savePath + "/" + file.key.Substring(folderModel.key.Length);
               FileInfo fileInfo = new FileInfo(fileName);
               if (!Directory.Exists(fileInfo.DirectoryName))
                   Directory.CreateDirectory(fileInfo.DirectoryName);

               await downloadfile(file, fileName);
           }
       }



       //async Task downloadFolder(string bucketName, string key, string savePath, System.Action<HttpProcessData> callback = null)
       //{
       //    if (!Directory.Exists(savePath))
       //        Directory.CreateDirectory(savePath);

       //    FolderModel folderModel = await folderListModel.getFolderModel(bucketName, key);

       //    IEnumerable<OssObjectSummary> list = folderModel.objList;

       //    List<Task> taskList = new List<Task>();

       //    foreach (FileModel file in folderModel.objList)
       //    {
       //        taskList.Add(downloadfile(file.bucketName, file.key, savePath + "/" + file.key.Substring(folderModel.key.Length), callback));
       //    }



       //    foreach (FolderModel folder in folderModel.folderList)
       //    {
       //        taskList.Add(downloadFolder(folder.bucketName, folder.key, savePath + "/" + folder.key.Substring(folderModel.key.Length), callback));
       //    }

       //    await Task.WhenAll(taskList);

       //}

       public async void download()
       {
            if (selectedIndex < 0)
             {


             }
             else
             {
                string foulderPath = fileFolderDialogService.openFolderDialog();
                if (foulderPath != null)
                {
                    if (foulderPath[foulderPath.Length - 1] != System.IO.Path.DirectorySeparatorChar)
                    {
                        foulderPath += System.IO.Path.DirectorySeparatorChar;
                    }

                    ObjectModel objModel = objectList[selectedIndex];
                    if (objModel != null)
                    {

                        if (objModel is FileModel)
                        {

                           
                            string fileName = foulderPath  + objModel.key.Substring(currentFolder.key.Length);
                            events.Publish(new TaskEvent(objModel, TaskEventType.DOWNLOADING));
                            ((FileModel)objModel).startTimer();
                            await downloadfile((FileModel)objModel, fileName);
                            events.Publish(new TaskEvent(objModel, TaskEventType.DOWNLOADCOMPELETED));
                        }
                        else
                        {
                            await folderListModel.initFolderForDownload((FolderModel)objModel);

                            events.Publish(new TaskEvent(objModel, TaskEventType.DOWNLOADING));
                            ((FolderModel)objModel).startTimer();
                            await downloadFolder((FolderModel)objModel, foulderPath + objModel.key.Substring(currentFolder.key.Length));
                            events.Publish(new TaskEvent(objModel, TaskEventType.DOWNLOADCOMPELETED));
                        }
                    }
                }

             }
       }

       private async Task uploadSingleFile(FileModel fileModel)
       {
           FileInfo fileInfo = new FileInfo(fileModel.localPath);
           FileStream fs = new FileStream(fileModel.localPath, FileMode.Open);
           ObjectMetadata oMetaData = new ObjectMetadata();
           await folderListModel.client.PutObject(fileModel.bucketName, fileModel.key, fs, oMetaData, fileModel.callback);
           fs.Dispose();
       }

       private  FolderModel uploadfolderInit(string bucket, string parentKey, string dir)
       {

           DirectoryInfo dirInfo = new DirectoryInfo(dir);

           FolderModel folderModel = new FolderModel();
           folderModel.objListAll = new List<FileModel>();
           folderModel.Size = 0;
           folderModel.bucketName = bucket;
           folderModel.key = parentKey + dirInfo.Name + "/";

         //  List<FileModel> result = new List<FileModel>();
         //  DirectoryInfo dirInfo = new DirectoryInfo(dir);

         //  string currentKey = parentKey + dirInfo.Name + "/";

           //await createFolder(bucket, currentKey);

           FileInfo[] fileInfos = dirInfo.GetFiles();
           foreach (FileInfo fileInfo in fileInfos)
           {
               folderModel.objListAll.Add(new FileModel() { bucketName = folderModel.bucketName, key = folderModel.key + fileInfo.Name, localPath = fileInfo.FullName });
               folderModel.Size += fileInfo.Length;
           }

           DirectoryInfo[] sonDirInfos = dirInfo.GetDirectories();
           foreach (DirectoryInfo sonDirInfo in sonDirInfos)
           {
               FolderModel model = uploadfolderInit(bucket, folderModel.key, sonDirInfo.FullName);
               folderModel.objListAll.AddRange(model.objListAll);
               folderModel.Size += model.Size;
           }

           return folderModel;

       }


       private List<FileModel> uploadfolderGetALLFile(string bucket, string parentKey, string localDir)
       {
           List<FileModel> result = new List<FileModel>();
           DirectoryInfo dirInfo = new DirectoryInfo(localDir);


           string currentKey = parentKey + dirInfo.Name + "/";

           FileInfo[] fileInfos = dirInfo.GetFiles();
           foreach (FileInfo fileInfo in fileInfos)
           {
               result.Add(new FileModel() { bucketName = bucket, key = currentKey + fileInfo.Name, localPath = fileInfo.FullName });
           }

           DirectoryInfo[] sonDirInfos = dirInfo.GetDirectories();
           foreach (DirectoryInfo sonDirInfo in sonDirInfos)
           {
               result.AddRange(uploadfolderGetALLFile(bucket, currentKey, sonDirInfo.FullName));
           }

           return result;

       }

       private async Task createFolders(FolderModel folderModel, string localDir)
       {
           DirectoryInfo dirInfo = new DirectoryInfo(localDir);

           
           await createFolder(folderModel.bucketName, folderModel.key);

           DirectoryInfo[] sonDirInfos = dirInfo.GetDirectories();
           foreach (DirectoryInfo sonDirInfo in sonDirInfos)
           {
               await createFolders(new FolderModel() { bucketName = folderModel.bucketName, key = folderModel.key + sonDirInfo.Name + "/" }, 
                   sonDirInfo.FullName);
           }
       }



    
        private async Task uploadFileInCurrentFolder(string fileName)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            FileModel objModel = new FileModel() { bucketName = currentFolder.bucketName, key = currentFolder.key + fileInfo.Name };
            objModel.localPath = fileName;
            events.Publish(new TaskEvent(objModel, TaskEventType.UPLOADING));
            objModel.startTimer();
            await uploadSingleFile(objModel);
            events.Publish(new TaskEvent(objModel, TaskEventType.UPLOADCOMPELETED));
        }


        async Task uploadFolder(FolderModel folderModel)
        {
            List<Task> taskList = new List<Task>();
            foreach (FileModel fileMode in folderModel.objListAll)
            {
               await uploadSingleFile(fileMode);
            }

            //await Task.WhenAll(taskList);

        }


       public async void uploadFileOperate()
       {
           if (currentFolder != null)
           {
               string[] fileNames = fileFolderDialogService.openFileDialog();
               if (fileNames != null)
               {
                   foreach (string fileName in fileNames)
                   {
                       await uploadFileInCurrentFolder(fileName);
                   }
                   refresh();

               }
           }
          

       }

       public async void uploadFolderOperate()
       {
           if (currentFolder != null)
           {
               string foulderPath = fileFolderDialogService.openFolderDialog();
               if (foulderPath != null)
               {

                   FolderModel folderModel = uploadfolderInit(currentFolder.bucketName, currentFolder.key, foulderPath);
                  

                   events.Publish(new TaskEvent(folderModel, TaskEventType.UPLOADING));
                   folderModel.startTimer();
                   await createFolders(folderModel, foulderPath);
                   await uploadFolder(folderModel);
                   events.Publish(new TaskEvent(folderModel, TaskEventType.UPLOADCOMPELETED));

                  
               }
               refresh();
           }

       }

       public async void uploadFolderZipOperate()
       {
           if (currentFolder != null)
           {
               string foulderPath = fileFolderDialogService.openFolderDialog();
               if (foulderPath != null)
               {
                   DirectoryInfo dirInfo = new DirectoryInfo(foulderPath);
                   string zipFileName = dirInfo.FullName + ".zip";
                   int i = 0;

                   while (File.Exists(zipFileName))
                   {
                       zipFileName = dirInfo.FullName + "_" + i.ToString() + ".zip";
                   }

                   ZipFile.CreateFromDirectory(foulderPath, zipFileName);

                   await uploadFileInCurrentFolder(zipFileName);               
               }
               refresh();
           }
       }
         public FolderListModel folderListModel;
         public FolderModel currentFolder;
         public BindableCollection<ObjectModel> objectList { get; set; }
         public History history{get;set;}
    }
}
