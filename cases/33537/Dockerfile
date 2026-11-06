FROM ubuntu:24.04@sha256:fe81560990fae86dc4743202b2cb4d3df98d0bcf949173cecef15ce1127ef852

ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates clang libclang-rt-dev gcc g++ make perl git \
    && rm -rf /var/lib/apt/lists/*
COPY cms_cfb.c /reproducer/cms_cfb.c
COPY run-in-container.sh /usr/local/bin/run-in-container
RUN chmod 0755 /usr/local/bin/run-in-container
ENTRYPOINT ["/usr/local/bin/run-in-container"]
