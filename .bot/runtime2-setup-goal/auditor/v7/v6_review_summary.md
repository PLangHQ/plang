# Auditor v6 Review Summary

Auditor v6 flagged LoadFromDirectoryAsync loading all .pr files (originally from security audit). Coder replaced it with Setup.DiscoverAsync — scans .pr files but only keeps IsSetup goals. Non-setup goals discarded, remain lazy-loadable.
