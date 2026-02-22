# Security Audit — data-envelope-architecture

**v1** — Full blue team + red team analysis. 12 findings (4 high, 6 medium, 2 low). Critical pattern: unbounded recursion in 5 locations. See [v1/summary.md](v1/summary.md).

**v2** — Post-review update. Creator corrected the threat model: PLang is user-sovereign, .pr files are trusted, the real trust boundary is cryptographic signatures. #4 and #5 reclassified as by-design. #9 (Verified settable bool) upgraded to HIGH — undermines the signature trust boundary. #8 downgraded to LOW. Recursion findings (#1-3, #7, #10) remain real. See [v2/summary.md](v2/summary.md).
