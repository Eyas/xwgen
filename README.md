# Algorithmic Crossword Generator

This repository contains the code for generating crossword puzzles as discussed in the blog post:
[Algorithmically Generated Crosswords: Building something 'good enough' for an NP-Complete problem](https://blog.eyas.sh/2025/12/algorithmic-crosswords/).

## Usage

To generate a crossword grid:

```bash
go run ./cmd/xwcli/ --file=testdata/words.txt --width=5
```

Run with `-help` for all options.
