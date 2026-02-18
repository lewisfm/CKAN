using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CKAN.Versioning;

namespace CKAN
{
    /// <summary>
    /// Access to the contents of CKAN's locally-stored repositories.
    /// </summary>
    public interface IRepositoryAccess
    {
        /// <summary>
        /// Fetch all on-device repositories, ordered by name.
        /// </summary>
        /// <returns>A list of all repositories that exist on-device.</returns>
        public IEnumerable<Repository> AllRepos();

        /// <summary>
        /// Create a new repository with the given name. Any previous repository
        /// with the same name will be overwritten.
        /// </summary>
        /// <param name="newRepo">The details of the new repository.</param>
        /// <returns>The numerical ID of the created repo.</returns>
        public long CreateEmptyRepo(RepositoryRef newRepo);

        /// <summary>
        /// Create a new repository using parsed data.
        /// </summary>
        /// <param name="repo">The metadata of the new repository.</param>
        /// <param name="repoData">The complete details of the new repository.</param>
        /// <returns>The numerical ID of the created repo.</returns>
        public long CreateRepo(RepositoryDto repo, RepositoryData repoData);

        /// <summary>
        /// Register a module with the given name. This will never overwrite any
        /// module, it just ensures one exists and returns its ID. If a new module
        /// is created, it won't have any releases.
        /// </summary>
        /// <param name="repoId">The ID of the repo to register it in.</param>
        /// <param name="slug">The textual ID (slug) of the new module.</param>
        /// <returns>The numerical ID of the registered module.</returns>
        public long RegisterModule(long repoId, string slug);

        /// <summary>
        /// Register a release for a module using deserialized data.
        /// </summary>
        /// <remarks>
        /// If the caller does not pass in the ID of the module responsible for this new release, this method will
        /// determine it automatically, adding the module to the database if it didn't already exist.
        /// </remarks>
        /// <param name="data">The release's metadata (name, version, summary, etc.)</param>
        /// <param name="repoId">The ID of the repository to create this release in.</param>
        /// <param name="moduleId">The ID of the module containing this release (if it's already known).</param>
        /// <returns></returns>
        public long CreateRelease(ReleaseDto data, long repoId, long? moduleId = null);

        /// <summary>
        /// Attach the given download counts to their corresponding modules. Creates
        /// the modules if they don't exist yet.
        /// </summary>
        /// <param name="repoId">The ID of the repository responsible for these download counts.</param>
        /// <param name="downloadCounts">
        /// A list of module slugs and the corresponding download counts for those modules.
        /// </param>
        public void AttachDownloadCounts(long repoId, IEnumerable<KeyValuePair<string, long>> downloadCounts);

        /// <summary>
        /// Record an endorsement from a locally-downloaded repository of a remote one.
        /// </summary>
        /// <param name="referrerId">The ID of the repository responsible for the reference</param>
        /// <param name="reference">The details of the remote repository which is being referred</param>
        public void CreateRepoReference(long referrerId, RepositoryRef reference);

        /// <summary>
        /// Returns a list of all modules tracked by the given repositories which have the specified slug.
        /// Repositories are prioritized first by their configured priority, then by name.
        /// </summary>
        /// <param name="repoIds">The IDs of the repositories to search in</param>
        /// <param name="slug">The unique-per-repository textual ID of the module to search for</param>
        /// <returns>A list of the requested modules</returns>
        public IEnumerable<Module> GetAvailableModules(IEnumerable<long> repoIds, string slug);

        /// <summary>
        /// Returns a list of all modules tracked by the given repositories.
        /// Repositories are prioritized first by their configured priority, then by name.
        /// </summary>
        /// <param name="repoIds">The IDs of the repositories to search in</param>
        /// <returns>A list of the requested modules</returns>
        public IEnumerable<Module> GetAllAvailableModules(IEnumerable<long> repoIds);

        /// <summary>
        /// Fetches the download count of the given module from one of the specified repositories.
        /// Repositories are prioritized first by their configured priority, then by name.
        /// </summary>
        /// <param name="repoIds">The IDs of the repositories to search in</param>
        /// <param name="slug">The unique-per-repository textual ID of the module to search for</param>
        /// <returns>
        /// The download count as reported by the highest priority repository, or null if none of the repositories
        /// track the specified module.
        /// </returns>
        public long? GetDownloadCount(IEnumerable<long> repoIds, string slug);

        /// <summary>
        /// Provides access to the underlying in-memory representation of the repository's data as an escape hatch.
        /// </summary>
        /// <returns>The entire contents of the repository loaded into memory.</returns>
        /// <exception cref="UnsupportedKraken">
        /// Thrown if this access method doesn't support loading the entire repository into memory.
        /// </exception>
        public RepositoryData? GetInternalRepoData(RepositoryDto repo);

        /// <summary>
        /// Provides access to the underlying in-memory representation of all tracked repositories as an escape hatch.
        /// </summary>
        /// <returns>The entire contents of each repository loaded into memory.</returns>
        /// <exception cref="UnsupportedKraken">
        /// Thrown if this access method doesn't support loading entire repositories into memory.
        /// </exception>
        public IEnumerable<RepositoryData> GetInternalRepoData();
    }

    /// <summary>
    /// A repository tracker which provides access to data without using an underlying storage method.
    /// </summary>
    public class InMemoryDataAccess : IRepositoryAccess
    {
        /// <summary>
        /// Creates an empty repository tracker.
        /// </summary>
        public InMemoryDataAccess() {}

        private readonly Dictionary<RepositoryDto, TrackedRepo> repositoriesData = new();
        private readonly IdTracker<RepositoryDto> trackedRepos = new();

        /// <summary>
        /// Returns an iterator over all known repositories and their keys, sorted by priority.
        /// </summary>
        private IEnumerable<KeyValuePair<long, RepositoryDto>> AllTrackedRepos() =>
            repositoriesData.Keys
                .OrderBy(dto => dto.priority)
                .ThenBy(dto => dto.name)
                .Select(dto => // (This is a fairly cheap operation since KVPs are structs.)
                    new KeyValuePair<long, RepositoryDto>(trackedRepos.LookupOrRegister(dto), dto)
                );

        /// <summary>
        /// Looks up several repositories by ID, sorted by priority.
        /// </summary>
        private IEnumerable<KeyValuePair<long, RepositoryDto>> GetReposById(IEnumerable<long> repos) =>
            // There is an assumption here that there aren't enough repositories for this to be a performance issue.
            AllTrackedRepos()
                .Where(repo => repos.Contains(repo.Key));

        public IEnumerable<Repository> AllRepos() =>
            AllTrackedRepos()
                .Select(pair => new Repository(pair.Value.name, pair.Value.uri)
                {
                    Id = pair.Key,
                    Priority = pair.Value.priority,
                });

        public long CreateEmptyRepo(RepositoryRef newRepo)
        {
            var repo = newRepo.AsDto();
            var repoData = new RepositoryData(
                Enumerable.Empty<ReleaseDto>(),
                new SortedDictionary<string, int>(),
                Enumerable.Empty<GameVersion>(),
                Enumerable.Empty<RepositoryDto>(),
                false
            );

            repositoriesData[repo] = new TrackedRepo(repoData);
            return trackedRepos.LookupOrRegister(repo);
        }

        public long CreateRepo(RepositoryDto repo, RepositoryData repoData)
        {
            repositoriesData[repo] = new TrackedRepo(repoData);
            return trackedRepos.LookupOrRegister(repo);
        }

        public long RegisterModule(long repoId, string slug)
        {
            var repo = trackedRepos.GetById(repoId);
            var repoData = repositoriesData[repo];

            // It's unclear if this can really ever happen in practice, but since the field is readonly there's not
            // much we can do to recover in this scenario.
            if (repoData.Storage.AvailableModules == null)
                throw new InvalidOperationException("Unexpected null module dictionary");

            // If this module already exists, don't forget about the old instance - just reuse it.
            var module = repoData.Storage.AvailableModules.GetValueOrDefault(slug)
                         ?? new ModuleDto(slug, Enumerable.Empty<ReleaseDto>());

            // Store the module, if we need to do that.
            if (!repoData.Storage.AvailableModules.ContainsKey(slug))
            {
                repoData.Storage.AvailableModules[slug] = module;
            }

            return repoData.TrackedModules.LookupOrRegister(module);
        }

        public long CreateRelease(ReleaseDto data, long repoId, long? moduleId = null)
        {
            var repo = trackedRepos.GetById(repoId);
            var repoData = repositoriesData[repo];

            // Get the module either by ID (if already known) or by looking up the module's name.
            var module = moduleId is { } id
                ? repoData.TrackedModules.GetById(id)
                : repoData.Storage.AvailableModules?.GetValueOrDefault(data.identifier);

            // If the module hasn't been created yet, do that now.
            module ??= repoData.TrackedModules.GetById(RegisterModule(repoId, data.identifier));

            if (module.module_version.ContainsKey(data.version))
            {
                throw new InvalidOperationException("Release already exists");
            }

            module.module_version[data.version] = data;

            return repoData.TrackedReleases.LookupOrRegister(data);
        }

        // In the future (other IRepositoryAccess impls), these two methods will allow repositories to be inserted into
        // storage without having the entire RepositoryDto in-memory at once. - i.e. repositories could be streamed into
        // storage as they are progressively unzipped.
        public void AttachDownloadCounts(long repoId, IEnumerable<KeyValuePair<string, long>> downloadCounts)
        {
            throw new UnsupportedKraken("Download counts are read-only with this access scheme");
        }

        public void CreateRepoReference(long referrerId, RepositoryRef reference)
        {
            throw new UnsupportedKraken("Repo refs are read-only with this access scheme");
        }

        public IEnumerable<Module> GetAvailableModules(IEnumerable<long> repoIds, string slug) =>
            GetReposById(repoIds) // Look up the module in each repo
                .Select<KeyValuePair<long, RepositoryDto>, Module?>(kvp =>
                {
                    var repoData = repositoriesData[kvp.Value];

                    // Check if the repo has this module.
                    var module = repoData.Storage.AvailableModules?.GetValueOrDefault(slug);
                    if (module == null) return null;

                    // If it does have the module, ask our ID tracker to assign the module an ID if necessary.
                    var moduleId = repoData.TrackedModules.LookupOrRegister(module);

                    // Database representation.
                    return new Module(kvp.Key, moduleId, module.identifier)
                    {
                        DownloadCount = repoData.Storage.DownloadCounts?.GetValueOrDefault(module.identifier) ?? 0,
                    };
                })
                .OfType<Module>();

        public IEnumerable<Module> GetAllAvailableModules(IEnumerable<long> repoIds) =>
            GetReposById(repoIds)
                .SelectMany(kvp =>
                {
                    var repoData = repositoriesData[kvp.Value];
                    var modules = repoData.Storage.AvailableModules?.Values
                                  ?? Enumerable.Empty<ModuleDto>();

                    return modules.Select(module =>
                    {
                        var moduleId = repoData.TrackedModules.LookupOrRegister(module);
                        return new Module(kvp.Key, moduleId, module.identifier)
                        {
                            DownloadCount = repoData.Storage.DownloadCounts?.GetValueOrDefault(module.identifier) ?? 0,
                        };
                    });
                });

        public long? GetDownloadCount(IEnumerable<long> repoIds, string slug)
        {
            var module = GetAvailableModules(repoIds, slug)
                .FirstOrDefault(mod => mod.DownloadCount != 0);
            return module?.DownloadCount;
        }

        public RepositoryData? GetInternalRepoData(RepositoryDto repo)
        {
            return repositoriesData.GetValueOrDefault(repo)?.Storage;
        }

        public IEnumerable<RepositoryData> GetInternalRepoData()
        {
            return repositoriesData.Values.Select(repo => repo.Storage);
        }
        
        /// <summary>
        /// Assigns and tracks IDs for a category of object in the internal data model. 
        /// </summary>
        /// <remarks>
        /// In this access scheme, numeric IDs are implemented on top of hierarchical dictionaries and are transient
        /// between CKAN's runs. Using the ID system is optional (but recommended) and can be bypassed by retrieving a
        /// RepositoryData instance or data transfer object (object with "Dto" suffix).
        /// </remarks>
        private class IdTracker<T> : IEnumerable<KeyValuePair<long, T>>
            where T : class, IEquatable<T>
        {
            private static long nextId = 0;

            // This class doesn't hold ownership over the objects it tracks; it just keeps track of extra metadata.
            // Thus, everything is held by weak reference.
            private Dictionary<long, WeakReference<T>> objectsById = new();
            private ConditionalWeakTable<T, StrongBox<long>> idsByObject = new();

            /// <summary>
            /// Looks up an object in the tracker by its numerical ID, throwing if it doesn't exist.
            /// </summary>
            public T GetById(long id)
            {
                var reference = objectsById[id];
                return reference.TryGetTarget(out var target)
                    ? target
                    : throw new KeyNotFoundException("Object has expired");
            }

            /// <summary>
            /// Looks up an object in the tracker by its numerical ID.
            /// </summary>
            public T? TryGetById(long id)
            {
                if (!objectsById.TryGetValue(id, out var reference)) return null;
                if (!reference.TryGetTarget(out var target))
                {
                    objectsById.Remove(id);
                    return null;
                }

                return target;
            }

            /// <summary>
            /// Returns the ID of the given object, assigning one if necessary.
            /// </summary>
            public long LookupOrRegister(T obj)
            {
                if (idsByObject.TryGetValue(obj, out var existingId))
                {
                    return existingId.Value;
                }

                long id;

                // Is this an alias of an equivalent object?
                try
                {
                    // Remember that this is an alias.
                    id = this.First(kvp => kvp.Value.Equals(obj)).Key;
                }
                catch (InvalidOperationException)
                {
                    // This object isn't tracked, so register it now.
                    id = nextId;
                    nextId += 1;
                }

                idsByObject.Add(obj, new StrongBox<long>(id));
                return id;
            }

            /// <summary>
            /// Iterate over all the tracked items and their IDs.
            /// </summary>
            public IEnumerator<KeyValuePair<long, T>> GetEnumerator()
            {
                return objectsById
                    .Select(kvp =>
                    {
                        KeyValuePair<long, T>? pair = null;
                        if (kvp.Value.TryGetTarget(out var target))
                            pair = new KeyValuePair<long, T>(kvp.Key, target);
                        return pair;
                    })
                    .OfType<KeyValuePair<long, T>>()
                    .GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private class TrackedRepo
        {
            public readonly IdTracker<ModuleDto> TrackedModules = new();
            public readonly IdTracker<ReleaseDto> TrackedReleases = new();

            /// <summary>
            /// In-memory representation of the repository's data.
            /// </summary>
            public readonly RepositoryData Storage;

            public TrackedRepo(RepositoryData storage)
            {
                Storage = storage;
            }
        }
    }
}