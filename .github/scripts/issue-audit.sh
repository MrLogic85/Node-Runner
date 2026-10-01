#!/usr/bin/env bash
# List open issues that break the rules in docs/ISSUE_LABELS.md ("Required metadata", "Size"
# and "Status").
#
#   issue-audit.sh        print one line per issue with what it is missing; exit 1 if any
#
# A leaf issue (no sub-issues) needs a Size unless it is an Idea, a `type: question` or a
# `type: epic`; a parent issue must not have one. A Blocked issue needs an open blocked-by
# issue, and a Ready or In progress one must have none. Uses the caller's gh login, whose token
# needs the `project` scope (gh auth refresh -s project).
set -euo pipefail

OWNER=MrLogic85
REPO=Node-Runner
PROJECT_NUMBER=2

[[ $# -eq 0 ]] || { sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

# Without access to the project every issue would look unassigned, so fail loudly instead.
gh api graphql -f owner=$OWNER -F number=$PROJECT_NUMBER -f query='
query($owner:String!,$number:Int!){ user(login:$owner){ projectV2(number:$number){ id } } }' \
  --jq '.data.user.projectV2.id' >/dev/null 2>&1 \
  || { echo "Cannot read project $OWNER/$PROJECT_NUMBER with this gh login (needs access and the project scope)." >&2; exit 2; }

problems=$(gh api graphql --paginate -f owner=$OWNER -f repo=$REPO -f query='
query($owner:String!,$repo:String!,$endCursor:String){ repository(owner:$owner,name:$repo){
  issues(states:OPEN,first:100,after:$endCursor){ pageInfo{ hasNextPage endCursor }
    nodes{ number title labels(first:30){ nodes{ name } } subIssuesSummary{ total }
      blockedBy(first:20){ nodes{ number state } }
      projectItems(first:10){ nodes{ project{ number }
        fieldValues(first:20){ nodes{
          ... on ProjectV2ItemFieldSingleSelectValue{ name field{ ... on ProjectV2FieldCommon{ name } } }
          ... on ProjectV2ItemFieldNumberValue{ number field{ ... on ProjectV2FieldCommon{ name } } } } } } } } } } }' \
  --jq '.data.repository.issues.nodes[]' |
  jq -rs --argjson p $PROJECT_NUMBER '
    .[] | ([.labels.nodes[].name]) as $labels
    | ([$labels[] | select(startswith("type: "))]) as $types
    | ([.projectItems.nodes[] | select(.project.number == $p)]) as $items
    | ([$items[].fieldValues.nodes[] | select(.field.name != null) | {(.field.name): (.name // .number)}] | add // {}) as $f
    | (.subIssuesSummary.total > 0) as $parent
    | ([.blockedBy.nodes[] | select(.state == "OPEN") | "#\(.number)"]) as $blockers
    | ($f.Status == "Idea" or ($types | index("type: question")) or ($types | index("type: epic"))) as $unsized
    | [ (if ($types | length) != 1 then "exactly one type label" else empty end),
        (if ([$labels[] | select(startswith("area: "))] | length) == 0 then "an area label" else empty end),
        (if ($items | length) == 0 then "to be in the project" else empty end),
        (if ($items | length) > 0 and $f.Status == null then "a Status" else empty end),
        (if ($items | length) > 0 and $f.Priority == null then "a Priority" else empty end),
        (if ($items | length) > 0 and ($parent | not) and ($unsized | not) and $f.Size == null then "a Size" else empty end),
        (if $parent and $f.Size != null then "no Size (it is a parent)" else empty end),
        (if $f.Status == "Blocked" and ($blockers | length) == 0
          then "an open blocked-by issue, or a Status other than Blocked" else empty end),
        (if ($f.Status == "Ready" or $f.Status == "In progress") and ($blockers | length) > 0
          then "Status Blocked (blocked by \($blockers | join(", ")))" else empty end) ] as $missing
    | select($missing | length > 0)
    | "#\(.number) needs \($missing | join(", ")): \(.title)"' |
  sort -t'#' -k2 -n)
if [[ -n $problems ]]; then
  echo "$problems"
  exit 1
fi
echo "All open issues have their required metadata."
