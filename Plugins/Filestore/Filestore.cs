﻿using Logshark.ArtifactProcessors.TableauServerLogProcessor.Parsers;
using Logshark.ArtifactProcessors.TableauServerLogProcessor.PluginInterfaces;
using Logshark.PluginLib.Extensions;
using Logshark.PluginLib.Model.Impl;
using Logshark.PluginLib.Persistence;
using Logshark.PluginModel.Model;
using Logshark.Plugins.Filestore.Helpers;
using Logshark.Plugins.Filestore.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Logshark.Plugins.Filestore
{
    public class Filestore : BaseWorkbookCreationPlugin, IServerClassicPlugin, IServerTsmPlugin
    {
        private PluginResponse pluginResponse;

        private Guid logsetHash;

        private IPersister<FilestoreEvent> filestorePersister;

        public override ISet<string> CollectionDependencies
        {
            get
            {
                return new HashSet<string>
                {
                    ParserConstants.FilestoreCollectionName
                };
            }
        }

        public override ICollection<string> WorkbookNames
        {
            get
            {
                return new List<string>
                {
                    "Filestore.twb"
                };
            }
        }

        public override IPluginResponse Execute(IPluginRequest pluginRequest)
        {
            pluginResponse = CreatePluginResponse();
            logsetHash = pluginRequest.LogsetHash;

            // Process Filestore events.
            IMongoCollection<BsonDocument> filestoreCollection = MongoDatabase.GetCollection<BsonDocument>(ParserConstants.FilestoreCollectionName);

            filestorePersister = GetConcurrentBatchPersister<FilestoreEvent>(pluginRequest);
            long totalFilestoreEvents = CountFilestoreEvents(filestoreCollection);
            using (GetPersisterStatusWriter<FilestoreEvent>(filestorePersister, totalFilestoreEvents))
            {
                ProcessFilestoreLogs(filestoreCollection);
            }
            Log.Info("Finished processing Filestore events!");

            // Check if we persisted any data.
            if (!PersistedData())
            {
                Log.Info("Failed to persist any data from Filestore logs!");
                pluginResponse.GeneratedNoData = true;
            }

            return pluginResponse;
        }

        protected IAsyncCursor<BsonDocument> GetFilestoreCursor(IMongoCollection<BsonDocument> collection)
        {
            var queryRequestsByFile = MongoQueryFilestoreHelper.FilestoreByFile(collection);
            var ignoreUnusedFieldsProjection = MongoQueryFilestoreHelper.IgnoreUnusedFilestoreFieldsProjection();
            return collection.Find(queryRequestsByFile).Project(ignoreUnusedFieldsProjection).ToCursor();
        }

        protected void ProcessFilestoreLogs(IMongoCollection<BsonDocument> collection)
        {
            GetOutputDatabaseConnection().CreateOrMigrateTable<FilestoreEvent>();

            Log.Info("Queueing Filestore events for processing..");

            // Construct a cursor to the requests to be processed.
            var cursor = GetFilestoreCursor(collection);
            var tasks = new List<Task>();
            while (cursor.MoveNext())
            {
                tasks.AddRange(cursor.Current.Select(document => Task.Factory.StartNew(() => ProcessFilestoreRequest(document))));
            }
            Task.WaitAll(tasks.ToArray());

            filestorePersister.Shutdown();
        }

        protected void ProcessFilestoreRequest(BsonDocument mongoDocument)
        {
            try
            {
                FilestoreEvent filestoreRequest = new FilestoreEvent(mongoDocument, logsetHash);
                filestorePersister.Enqueue(filestoreRequest);
            }
            catch (Exception ex)
            {
                string errorMessage = String.Format("Encountered an exception on {0}: {1}", mongoDocument.GetValue("_id"), ex);
                pluginResponse.AppendError(errorMessage);
                Log.Error(errorMessage);
            }
        }

        /// <summary>
        /// Count the number of Filestore events in the collection.
        /// </summary>
        /// <param name="collection">The collection to search for requests in.</param>
        /// <returns>The number of Filestore Events in the collection</returns>
        protected long CountFilestoreEvents(IMongoCollection<BsonDocument> collection)
        {
            var query = MongoQueryFilestoreHelper.FilestoreByFile(collection);
            return collection.Count(query);
        }
    }
}