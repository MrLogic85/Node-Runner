# Splits a shell command line into simple commands, one per output line, with
# its words separated by \037 and quotes removed. Quoted text, comments,
# redirections and heredoc bodies yield no commands, so `echo "git stash"`
# reads as echo plus one word. Command and process substitutions start a
# command of their own. A best-effort reader for the guard hooks, not a shell.
{ src = src (NR > 1 ? "\n" : "") $0 }

END {
  n = length(src); i = 1; word = ""; inword = 0; line = ""; nwords = 0
  redirect = 0; heredoc = 0; npending = 0
  while (i <= n) {
    c = substr(src, i, 1)
    if (c == "'") {
      j = index(substr(src, i + 1), "'")
      if (j == 0) j = n - i + 1
      word = word substr(src, i + 1, j - 1); inword = 1; i += j + 1; continue
    }
    if (c == "\"") {
      i++
      while (i <= n && (d = substr(src, i, 1)) != "\"") {
        if (d == "\\" && i < n && index("\"\\$`\n", substr(src, i + 1, 1))) { i++; d = substr(src, i, 1) }
        word = word d; i++
      }
      inword = 1; i++; continue
    }
    if (c == "\\") {
      if (i < n && substr(src, i + 1, 1) != "\n") { word = word substr(src, i + 1, 1); inword = 1 }
      i += 2; continue
    }
    if (c == " " || c == "\t") { endword(); i++; continue }
    if (c == "#" && !inword) {
      while (i <= n && substr(src, i, 1) != "\n") i++
      continue
    }
    if (c == "<" || c == ">" || (c == "&" && substr(src, i + 1, 1) == ">")) {
      if (inword && word ~ /^[0-9]+$/) { word = ""; inword = 0 } else endword()
      op = c; i++
      while (i <= n && index("<>&-|", substr(src, i, 1)) && length(op) < 3) { op = op substr(src, i, 1); i++ }
      if (substr(src, i, 1) == "(" && (c == "<" || c == ">") && length(op) == 1) { i++; endcommand(); continue }
      redirect = 1; heredoc = (op == "<<" || op == "<<-")
      strip = (op == "<<-")
      continue
    }
    if (c == "$" && substr(src, i + 1, 1) == "(") { endcommand(); i += 2; continue }
    if (c == "\n") {
      endcommand(); i++
      for (p = 1; p <= npending; p++) i = skipbody(i, pending[p], pendingstrip[p])
      npending = 0; continue
    }
    if (index(";&|()`", c)) { endcommand(); i++; continue }
    word = word c; inword = 1; i++
  }
  endcommand()
}

function endword() {
  if (!inword) return
  if (redirect) {
    if (heredoc) { npending++; pending[npending] = word; pendingstrip[npending] = strip }
    redirect = 0; heredoc = 0
  } else {
    gsub(/\n/, " ", word)
    line = (nwords ? line "\037" : "") word; nwords++
  }
  word = ""; inword = 0
}

function endcommand() {
  endword()
  if (nwords) print line
  line = ""; nwords = 0; redirect = 0; heredoc = 0
}

function skipbody(at, delim, tabs,    stop, text) {
  while (at <= n) {
    stop = index(substr(src, at), "\n")
    text = stop ? substr(src, at, stop - 1) : substr(src, at)
    at = stop ? at + stop : n + 1
    if (tabs) sub(/^\t+/, "", text)
    if (text == delim) break
  }
  return at
}
