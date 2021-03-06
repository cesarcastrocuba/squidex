﻿// ==========================================================================
//  MongoEventStore.cs
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex Group
//  All rights reserved.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Squidex.Infrastructure.CQRS.Events;

// ReSharper disable RedundantAssignment
// ReSharper disable InvertIf
// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable TooWideLocalVariableScope

namespace Squidex.Infrastructure.MongoDb.EventStore
{
    public class MongoEventStore : MongoRepositoryBase<MongoEventCommit>, IEventStore
    {
        private static readonly BsonTimestamp EmptyTimestamp = new BsonTimestamp(0);
        private readonly IEventNotifier notifier;

        public MongoEventStore(IMongoDatabase database, IEventNotifier notifier) 
            : base(database)
        {
            Guard.NotNull(notifier, nameof(notifier));

            this.notifier = notifier;
        }

        protected override string CollectionName()
        {
            return "Events";
        }

        protected override MongoCollectionSettings CollectionSettings()
        {
            return new MongoCollectionSettings { ReadPreference = ReadPreference.Primary, WriteConcern = WriteConcern.WMajority };
        }

        protected override async Task SetupCollectionAsync(IMongoCollection<MongoEventCommit> collection)
        {
            await collection.Indexes.CreateOneAsync(Index.Ascending(x => x.Timestamp).Ascending(x => x.EventStream));
            await collection.Indexes.CreateOneAsync(Index.Ascending(x => x.EventStreamOffset).Ascending(x => x.EventStream), new CreateIndexOptions { Unique = true });
        }

        public IObservable<StoredEvent> GetEventsAsync(string streamFilter = null, string position = null)
        {
            return Observable.Create<StoredEvent>((observer, ct) =>
            {
                return GetEventsAsync(storedEvent =>
                {
                    observer.OnNext(storedEvent);

                    return Tasks.TaskHelper.Done;
                }, ct, streamFilter, position);
            });
        }

        public async Task GetEventsAsync(Func<StoredEvent, Task> callback, CancellationToken cancellationToken, string streamFilter = null, string position = null)
        {
            Guard.NotNull(callback, nameof(callback));

            var tokenTimestamp = EmptyTimestamp;
            var tokenCommitSize = -1;
            var tokenCommitOffset = -1;
            var isEndOfCommit = false;

            if (position != null)
            {
                var token = ParsePosition(position);

                tokenTimestamp = token.Timestamp;
                tokenCommitSize = token.CommitSize;
                tokenCommitOffset = token.CommitOffset;

                isEndOfCommit = tokenCommitOffset == tokenCommitSize - 1;

                if (isEndOfCommit)
                {
                    tokenCommitOffset = -1;
                }
            }

            var filter = CreateFilter(streamFilter, isEndOfCommit, tokenTimestamp);

            await Collection.Find(filter).SortBy(x => x.Timestamp).ForEachAsync(async commit =>
            {
                var eventStreamNumber = (int)commit.EventStreamOffset;

                var commitOffset = 0;

                foreach (var e in commit.Events)
                {
                    eventStreamNumber++;

                    if (commitOffset > tokenCommitOffset)
                    {
                        var eventData = new EventData { EventId = e.EventId, Metadata = e.Metadata, Payload = e.Payload, Type = e.Type };
                        var eventToken = CreateToken(commit.Timestamp, commitOffset, commit.Events.Length);

                        await callback(new StoredEvent(eventToken, eventStreamNumber, eventData));

                        commitOffset++;
                    }
                    else
                    {
                        break;
                    }
                }
            }, cancellationToken);
        }

        public async Task AppendEventsAsync(Guid commitId, string streamName, int expectedVersion, ICollection<EventData> events)
        {
            Guard.NotNullOrEmpty(streamName, nameof(streamName));
            Guard.NotNull(events, nameof(events));

            var eventsCount = events.Count;

            if (eventsCount > 0)
            {
                var commitEvents = new MongoEvent[events.Count];

                var i = 0;

                foreach (var e in events)
                {
                    var mongoEvent = new MongoEvent { EventId = e.EventId, Metadata = e.Metadata, Payload = e.Payload, Type = e.Type };

                    commitEvents[i++] = mongoEvent;
                }

                var commit = new MongoEventCommit
                {
                    Id = commitId,
                    Events = commitEvents,
                    EventsCount = eventsCount,
                    EventStream = streamName,
                    EventStreamOffset = expectedVersion,
                    Timestamp = EmptyTimestamp
                };
                
                try
                {
                    await Collection.InsertOneAsync(commit);

                    notifier.NotifyEventsStored();
                }
                catch (MongoWriteException ex)
                {
                    if (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                    {
                        var currentVersion = await GetEventStreamOffset(streamName);

                        throw new WrongEventVersionException(currentVersion, expectedVersion);
                    }

                    throw;
                }
            }
        }

        private async Task<long> GetEventStreamOffset(string streamName)
        {
            var document =
                await Collection.Find(x => x.EventStream == streamName)
                    .Project<BsonDocument>(Project
                        .Include(x => x.EventStreamOffset)
                        .Include(x => x.EventsCount))
                    .SortByDescending(x => x.EventStreamOffset).Limit(1)
                    .FirstOrDefaultAsync();

            if (document != null)
            {
                return document["EventStreamOffset"].ToInt64() + document["EventsCount"].ToInt64();
            }

            return -1;
        }

        private static FilterDefinition<MongoEventCommit> CreateFilter(string streamFilter, bool isEndOfCommit, BsonTimestamp tokenTimestamp)
        {
            var filters = new List<FilterDefinition<MongoEventCommit>>();

            if (isEndOfCommit)
            {
                filters.Add(Filter.Gt(x => x.Timestamp, tokenTimestamp));
            }
            else
            {
                filters.Add(Filter.Gte(x => x.Timestamp, tokenTimestamp));
            }

            if (!string.IsNullOrWhiteSpace(streamFilter) && !string.Equals(streamFilter, "*", StringComparison.OrdinalIgnoreCase))
            {
                if (streamFilter.Contains("^"))
                {
                    filters.Add(Filter.Regex(x => x.EventStream, streamFilter));
                }
                else
                {
                    filters.Add(Filter.Eq(x => x.EventStream, streamFilter));
                }
            }

            FilterDefinition<MongoEventCommit> filter = new BsonDocument();

            if (filters.Count > 1)
            {
                filter = Filter.And(filters);
            }
            else if (filters.Count == 1)
            {
                filter = filters[0];
            }

            return filter;
        }

        private static string CreateToken(BsonTimestamp timestamp, int commitOffset, int commitSize)
        {
            var parts = new object[] { timestamp.Timestamp, timestamp.Increment, commitOffset, commitSize };

            return string.Join("-", parts);
        }

        private static (BsonTimestamp Timestamp, int CommitOffset, int CommitSize) ParsePosition(string position)
        {
            var parts = position.Split('-');

            return (new BsonTimestamp(int.Parse(parts[0]), int.Parse(parts[1])), int.Parse(parts[2]), int.Parse(parts[3]));
        }
    }
}
