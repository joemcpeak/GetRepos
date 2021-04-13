using System;
using System.Collections.Generic;
using System.Text;

namespace GetRepos
{
    class RepoInfo
    {
        public string ProjectName { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
        public CommitInfo LastCommit { get; set; }
    }
}
