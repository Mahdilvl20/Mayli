# Codebase Intelligence & Memory (codebase-memory-mcp)

This project is indexed with **codebase-memory-mcp** for instant, token-efficient architectural analysis, symbol searching, call tracing, and dependency mapping without manual multi-file reads.

- **Project Name in Memory**: `F-Vayli_Project`
- **Graph Storage**: `.codebase-memory/graph.db.zst`
- **MCP Server Binary**: `C:/Users/MisRay/AppData/Local/Programs/codebase-memory-mcp/codebase-memory-mcp.exe`
- **Global MCP Configuration**: `~/.gemini/config/mcp_config.json`

### Key Tools & Commands:
1. **Symbol & Code Search**:
   ```powershell
   codebase-memory-mcp cli search_graph --project "F-Vayli_Project" --query "<symbol_or_keyword>"
   ```
2. **Path & Call Trace**:
   ```powershell
   codebase-memory-mcp cli trace_path --project "F-Vayli_Project" --from "<caller>" --to "<callee>"
   ```
3. **File Outline**:
   ```powershell
   codebase-memory-mcp cli get_file_outline --project "F-Vayli_Project" --file "<path>"
   ```
4. **Cypher Graph Query**:
   ```powershell
   codebase-memory-mcp cli query_graph --project "F-Vayli_Project" --query "<cypher_query>"
   ```
5. **Update Index after Major Refactoring**:
   ```powershell
   codebase-memory-mcp cli index_repository --repo-path "F:\Vayli_Project" --mode fast --persistence true
   ```
