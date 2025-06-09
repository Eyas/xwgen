package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"strings"
	"time"

	"cloud.google.com/go/bigquery"
	"github.com/GoogleCloudPlatform/functions-framework-go/funcframework"
	"google.golang.org/api/iterator"

	xw_generator "xw_generator/generator"
)

type GenerateGridRequest struct {
	LineLength     int      `json:"lineLength"`
	WordScope      string   `json:"wordScope"`
	IncludeObscure bool     `json:"includeObscure"`
	PreferredWords []string `json:"preferredWords"`
	ObscureWords   []string `json:"obscureWords"`
	ExcludedWords  []string `json:"excludedWords"`
	MaxGrids       int      `json:"maxGrids"`
}

type GenerateGridResponse struct {
	Success bool     `json:"success"`
	Grids   []string `json:"grids"`
	Error   string   `json:"error,omitempty"`
}

func getWords(ctx context.Context, scope string, includeObscure bool) ([]string, []string, error) {
	client, err := bigquery.NewClient(ctx, "xword-x")
	if err != nil {
		return nil, nil, fmt.Errorf("bigquery.NewClient: %w", err)
	}
	defer client.Close()

	obscureValues := []string{"false"}
	if includeObscure {
		obscureValues = append(obscureValues, "true")
	}
	query := fmt.Sprintf("SELECT word_key, obscure FROM `xword-x.FirestoreQuery.all_words` WHERE scope = %q AND obscure IN (%s)", scope, strings.Join(obscureValues, ","))
	q := client.Query(query)
	q.Location = "US"

	job, err := q.Run(ctx)
	if err != nil {
		return nil, nil, fmt.Errorf("q.Run: %w", err)
	}
	status, err := job.Wait(ctx)
	if err != nil {
		return nil, nil, fmt.Errorf("job.Wait: %w", err)
	}
	if err := status.Err(); err != nil {
		return nil, nil, fmt.Errorf("status.Err: %w", err)
	}
	it, err := job.Read(ctx)
	if err != nil {
		return nil, nil, fmt.Errorf("job.Read: %w", err)
	}
	var regularWords []string
	var obscureWords []string

	for {
		var row []bigquery.Value
		err := it.Next(&row)
		if err == iterator.Done {
			break
		}
		if err != nil {
			return nil, nil, fmt.Errorf("it.Next: %w", err)
		}

		word, ok := row[0].(string)
		if !ok {
			return nil, nil, fmt.Errorf("row[0] is not a string: %v", row[0])
		}
		isObscure, ok := row[1].(bool)
		if !ok {
			return nil, nil, fmt.Errorf("row[1] is not a bool: %v", row[1])
		}
		if isObscure {
			obscureWords = append(obscureWords, word)
		} else {
			regularWords = append(regularWords, word)
		}
	}
	return regularWords, obscureWords, nil
}

func execute(ctx context.Context, req GenerateGridRequest) ([]string, error) {
	if req.LineLength < 3 {
		return nil, fmt.Errorf("lineLength must be at least 3")
	}
	if req.MaxGrids <= 0 {
		return nil, fmt.Errorf("maxGrids must be at least 1")
	}
	if req.MaxGrids > 10 {
		return nil, fmt.Errorf("maxGrids must be at most 10")
	}

	for i, word := range req.PreferredWords {
		req.PreferredWords[i] = strings.ToLower(word)
	}
	for i, word := range req.ObscureWords {
		req.ObscureWords[i] = strings.ToLower(word)
	}
	for i, word := range req.ExcludedWords {
		req.ExcludedWords[i] = strings.ToLower(word)
	}

	if req.WordScope != "" {
		regularWords, obscureWords, err := getWords(ctx, req.WordScope, req.IncludeObscure)
		if err != nil {
			return nil, fmt.Errorf("getWords: %w", err)
		}
		fmt.Printf("Loaded %d regular words and %d obscure words\n", len(regularWords), len(obscureWords))

		req.PreferredWords = append(req.PreferredWords, regularWords...)
		req.ObscureWords = append(req.ObscureWords, obscureWords...)
	}

	if len(req.PreferredWords) == 0 {
		return nil, fmt.Errorf("preferredWords must not be empty")
	}

	generator := xw_generator.CreateGenerator(
		req.LineLength,
		req.PreferredWords,
		req.ObscureWords,
		req.ExcludedWords,
	)

	// Generate grids
	var grids []string
	count := 0

	deadline, ok := ctx.Deadline()
	timeout := 1 * time.Minute
	if ok {
		timeout = time.Until(deadline) - 5*time.Second
		fmt.Printf("Setting timeout to %v\n", timeout)
	}

	ctx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	for grid := range generator.PossibleGrids(ctx) {
		fmt.Printf("Generated grid %d of %d\n", 1+count, req.MaxGrids)

		grids = append(grids, grid.Repr())
		count++
		if count >= req.MaxGrids {
			break
		}
	}

	return grids, ctx.Err()
}

func setCORSHeaders(w http.ResponseWriter) {
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("Access-Control-Allow-Headers", "Content-Type")
	w.Header().Set("Access-Control-Allow-Methods", "POST, OPTIONS")
	w.Header().Set("Content-Type", "application/json")
}

func generateGrid(w http.ResponseWriter, r *http.Request) {
	// Set CORS headers
	setCORSHeaders(w)

	// Handle OPTIONS request for CORS preflight
	if r.Method == "OPTIONS" {
		w.WriteHeader(http.StatusOK)
		return
	}

	if r.Method != "POST" {
		w.WriteHeader(http.StatusMethodNotAllowed)
		fmt.Fprintf(w, `{"success": false, "error": "Method %s not allowed"}`, r.Method)
		return
	}

	var req GenerateGridRequest

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		fmt.Printf("Error parsing JSON body: %v", err)
		w.WriteHeader(http.StatusBadRequest)
		response := GenerateGridResponse{
			Success: false,
			Error:   fmt.Sprintf("Invalid JSON: %v", err),
		}
		json.NewEncoder(w).Encode(response)
		return
	}

	grids, err := execute(r.Context(), req)

	response := GenerateGridResponse{
		Success: err == nil,
		Grids:   grids,
	}

	if err != nil {
		response.Error = err.Error()
	} else if len(grids) == 0 {
		response.Error = "No valid grids could be generated with the given parameters"
	}

	if err := json.NewEncoder(w).Encode(response); err != nil {
		fmt.Printf("Error marshaling response: %v", err)
		w.WriteHeader(http.StatusInternalServerError)
		fmt.Fprint(w, `{"success": false, "error": "Internal server error"}`)
		return
	}
}

func main() {
	funcframework.RegisterHTTPFunction("/generate-grid", generateGrid)

	port := "8080"
	if envPort := os.Getenv("PORT"); envPort != "" {
		port = envPort
	}
	hostname := ""
	if localOnly := os.Getenv("LOCAL_ONLY"); localOnly == "true" {
		hostname = "127.0.0.1"
	}
	if err := funcframework.StartHostPort(hostname, port); err != nil {
		log.Fatalf("funcframework.StartHostPort: %v\n", err)
	}
}
