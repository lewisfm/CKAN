using System;
using System.Collections.Generic;

namespace CKAN
{
    /// <summary>
    /// A repository of modules whose metadata is indexed on-device.
    /// </summary>
    public class Repository
    {
        /// <summary>
        /// The ID of the local copy of this repository (surrogate key).
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The unique textual identifier of the repository.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The URL which serves this repository's remote data.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// The priority by which data from this repository is used (e.g. when resolving
        /// a module by ID, or when fetching download counts). Repositories with the same
        /// configured priority are prioritized by name.
        /// </summary>
        public int Priority { get; set; }

        public Repository(string name, Uri uri)
        {
            Name = name;
            Uri = uri;
        }

        public RepositoryRef AsRef()
        {
            return new RepositoryRef(Name, Uri);
        }
    }

    /// <summary>
    /// A reference to a repository that may or may not be locally downloaded.
    /// This is logically just a URL and some metadata.
    /// </summary>
    public class RepositoryRef
    {
        /// <summary>
        /// The name of the referenced repository, unique per referrer (if there is one).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The URL which serves this repository's remote data.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// The suggested priority of this repository.
        /// </summary>
        public int Priority { get; set; }

        public RepositoryRef(string name, Uri uri)
        {
            Name = name;
            Uri = uri;
            Priority = 0;
        }

        /// <summary>
        /// Re-interprets this reference to a remote repository as a local repository with the given database ID.
        /// </summary>
        public Repository ToRepo(long id) => new(Name, Uri)
        {
            Id = id,
            Priority = Priority,
        };

        /// <summary>
        /// Converts this reference to a remote repository to an object which can be serialized.
        /// </summary>
        public RepositoryDto AsDto() => new(Name, Uri)
        {
            priority = Priority,
        };
    }
}