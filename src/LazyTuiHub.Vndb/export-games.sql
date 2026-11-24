-- Run with psql -XAt -v ON_ERROR_STOP=1 against the tuihub/vndb-db database.
SELECT DISTINCT ON (id) json_build_object(
    'provider', 'vndb', 'externalId', id::text, 'title', title,
    'url', 'https://vndb.org/' || id::text, 'alternativeTitle', latin
)::text
FROM vn_titles
ORDER BY id, (lang = 'ja') DESC, (lang = 'en') DESC, lang, title;
