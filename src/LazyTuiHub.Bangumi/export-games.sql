-- Run with psql -XAt -v ON_ERROR_STOP=1 against the tuihub/bangumi-db database.
SELECT json_build_object(
    'provider', 'bangumi', 'externalId', id::text, 'title', name,
    'url', 'https://bgm.tv/subject/' || id::text,
    'description', summary, 'releaseDate', date, 'alternativeTitle', name_cn
)::text
FROM public.subject
WHERE type = 4
ORDER BY id;
