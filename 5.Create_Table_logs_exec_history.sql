CREATE TABLE public.logs_exec_history
(
    id_start bigint NOT NULL,
    id_end bigint NOT NULL,
    exec_time timestamp without time zone,
    CONSTRAINT logs_exec_history_pkey PRIMARY KEY (id_start, id_end)
);
