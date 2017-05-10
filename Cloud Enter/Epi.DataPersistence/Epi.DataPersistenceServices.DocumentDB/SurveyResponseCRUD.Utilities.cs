﻿using Epi.Cloud.Common.Configuration;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Linq;
using Epi.Cloud.Common.Constants;
using System.Threading;
using System.Threading.Tasks;

namespace Epi.DataPersistenceServices.DocumentDB
{
    public partial class SurveyResponseCRUD
    {
        private string _serviceEndpoint;
        private string _authKey;
        private DocumentClient _client;
        private Microsoft.Azure.Documents.Database _database;
        private ConcurrentDictionary<string, DocumentCollection> _documentCollections = new ConcurrentDictionary<string, DocumentCollection>();

        private void Initialize()
        {
            lock (this)
            {
                ParseConnectionString();

                //Getting reference to Database 
                GetOrCreateDatabase(DatabaseName);
            }
        }

        private void ParseConnectionString()
        {
            var connectionString = ConfigurationHelper.GetEnvironmentResourceKey("CollectedDataConnectionString");

            string[] parts;

            if (connectionString.StartsWith("AccountEndpoint="))
            {
                parts = connectionString.Split(';');
            }
            else
            {
                connectionString = string.Concat("AccountEndpoint=", connectionString);
                parts = connectionString.Split(',');
            }

            for (var i = 0; i < parts.Length; ++i)
            {
                var nvp = parts[i];
                var eqSignIndex = nvp.IndexOf('=');
                var name = nvp.Substring(0, eqSignIndex).Trim().ToLowerInvariant();
                var value = nvp.Substring(eqSignIndex + 1).Trim();
                switch (name)
                {
                    case "accountendpoint":
                        _serviceEndpoint = value.Trim();
                        break;
                    case "accountkey":
                        _authKey = value;
                        break;
                    case "authkey":
                        _authKey = value;
                        break;
                    case "dbname":
                        DatabaseName = value;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(_serviceEndpoint) || string.IsNullOrWhiteSpace(_authKey))
            {
                throw new ConfigurationException("SurveyResponse ConnectionString is invalid. Service Endpoint and AuthKey must be specified.");
            }
        }

        #region GetOrCreateClient

        private DocumentClient GetOrCreateClient()
        {
            lock (this)
            {
                if (_client == null)
                {
#if DEBUG
                    _client = new DocumentClient(new Uri(_serviceEndpoint), _authKey);
#else
                    if (AppSettings.GetBoolValue(AppSettings.Key.IsLocalReleaseBuild))
                    {
                        _client = new DocumentClient(new Uri(_serviceEndpoint), _authKey);
                    }
                    else
                    {
                        _client = new DocumentClient(new Uri(_serviceEndpoint), _authKey,
                            new ConnectionPolicy
                            {
                                ConnectionMode = ConnectionMode.Direct,
                                ConnectionProtocol = Protocol.Tcp
                            });
                    }
#endif
                }
                return _client;
            }
        }
        #endregion

        #region GetOrCreateDabase
        /// <summary>
        ///If DB is not avaliable in Document Db create DB
        /// </summary>
        private Database GetOrCreateDatabase(string databaseName)
        {
            var client = GetOrCreateClient();

            lock (this)
            {
                if (_database == null)
                {
                    _database = client.CreateDatabaseQuery().Where(d => d.Id == databaseName).AsEnumerable().FirstOrDefault();
                    if (_database == null)
                    {
                        client.CreateDatabaseAsync(new Microsoft.Azure.Documents.Database { Id = databaseName }, null)
                            .ContinueWith(t => _database = t.Result); ;
                    }
                }
                return _database;
            }
        }

        #endregion

        #region GetOrCreateCollection 
        /// <summary>
        /// Get or Create Collection in Document DB
        /// </summary>
        private DocumentCollection GetOrCreateCollection(string databaseLink, string collectionId)
        {
            lock (this)
            {
                DocumentCollection documentCollection;
                if (!_documentCollections.TryGetValue(collectionId, out documentCollection))
                {
                    documentCollection = Client.CreateDocumentCollectionQuery(databaseLink).Where(c => c.Id == collectionId).AsEnumerable().FirstOrDefault();
                    if (documentCollection == null)
                    {
#if ConfigureIndexing
					DocumentCollection collectionSpec = ConfigureIndexing(collectionId);
#else
                        var collectionSpec = new DocumentCollection
                        {
                            Id = collectionId,
                        };
#endif //ConfigureIndexing
                        documentCollection = Client.CreateDocumentCollectionAsync(databaseLink, collectionSpec).Result;
                        _documentCollections[collectionId] = documentCollection;

                    }
                }
                return documentCollection;
            }
        }
        #endregion

        #region GetCollectionReference
        private DocumentCollection GetCollectionReference(string collectionId)
        {

            //Get a reference to the DocumentDB Collection
            var collection = GetOrCreateCollection(ResponseDatabase.SelfLink, collectionId);
            return collection;
        }
        #endregion

        #region GetCollectionUri
        private Uri GetCollectionUri(string collectionId)
        {
            GetCollectionReference(collectionId);
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, collectionId);
            return collectionUri;
        }
        #endregion

        public ResourceResponse<Document> ExecuteAsync(Func<Task<ResourceResponse<Document>>> asyncFunc, Action followOnAction = null)
        {
            using (ManualResetEvent completionEvent = new ManualResetEvent(false))
            {
                ResourceResponse<Document> result = null;

                Task<ResourceResponse<Document>> documentTask = null;

                var backgroundTask = Task.Run(() =>
                {
                    documentTask = asyncFunc();
                });

                var millisecondsToSleep = 100;
                var retries = (Int32)TimeSpan.FromSeconds(5).TotalMilliseconds / millisecondsToSleep;
                bool isCompleted = false;
                while (retries > 0)
                {
                    if (documentTask == null) { Thread.Sleep(10); continue; }
                    isCompleted = documentTask.IsCompleted;
                    if (isCompleted) break;
                    Thread.Sleep(millisecondsToSleep);
                    retries -= 1;
                }
                bool isSuccessful = isCompleted;

                var awaiter = documentTask.ContinueWith(t =>
                {
                    if (followOnAction != null)
                    {
                        followOnAction();
                    }
                    completionEvent.Set();
                }, TaskContinuationOptions.AttachedToParent).ConfigureAwait(false);

                isSuccessful &= completionEvent.WaitOne(TimeSpan.FromSeconds(5));

                awaiter.GetAwaiter().GetResult();

                result = documentTask.Result;

                isSuccessful &= (result != null);

                isSuccessful &= backgroundTask.Wait(TimeSpan.FromSeconds(5));

                return result;
            }
        }


        public bool ExecuteAsync(Func<Task<bool>> asyncFunc, Action followOnAction = null)
        {
            using (ManualResetEvent completionEvent = new ManualResetEvent(false))
            {
                bool result = false;

                Task<bool> boolTask = null;

                var backgroundTask = Task.Run(() =>
                {
                    boolTask = asyncFunc();
                });

                var millisecondsToSleep = 100;
                var retries = (Int32)TimeSpan.FromSeconds(5).TotalMilliseconds / millisecondsToSleep;
                bool isCompleted = false;
                while (retries > 0)
                {
                    if (boolTask == null) { Thread.Sleep(10); continue; }
                    isCompleted = boolTask.IsCompleted;
                    if (isCompleted) break;
                    Thread.Sleep(millisecondsToSleep);
                    retries -= 1;
                }
                bool isSuccessful = isCompleted;

                var awaiter = boolTask.ContinueWith(t =>
                {
                    if (followOnAction != null)
                    {
                        followOnAction();
                    }
                    completionEvent.Set();
                }, TaskContinuationOptions.AttachedToParent).ConfigureAwait(false);

                isSuccessful &= completionEvent.WaitOne(TimeSpan.FromSeconds(5));

                awaiter.GetAwaiter().GetResult();

                result = boolTask.Result;

                isSuccessful &= result;

                isSuccessful &= backgroundTask.Wait(TimeSpan.FromSeconds(5));

                return isSuccessful;
            }
        }

    }
}
