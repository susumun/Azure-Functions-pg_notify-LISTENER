CREATE TABLE public.logs_watermark
(
    id bigint NOT NULL,
    status text COLLATE pg_catalog."default",
    CONSTRAINT logs_watermark_pkey PRIMARY KEY (id)
);
