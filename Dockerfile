FROM ubuntu:16.04

RUN apt-key adv \
        --keyserver hkp://keyserver.ubuntu.com:80 \
        --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
    && echo "deb http://download.mono-project.com/repo/debian wheezy main" | \
        tee /etc/apt/sources.list.d/mono-xamarin.list \
    && apt-get update \
    && apt-get install -y \
        mono-complete \
        fsharp \
        python3-pip \
        git \
    && rm -rf /var/lib/apt/lists/*

RUN pip3 install --upgrade pip && pip3 install jupyter

WORKDIR /
RUN git clone https://github.com/fsprojects/IfSharp.git

WORKDIR /IfSharp
RUN git checkout jupyter
RUN ./build.sh
RUN mono bin/ifsharp.exe --install

WORKDIR /
RUN mkdir notebooks
VOLUME notebooks

EXPOSE 8888

ENTRYPOINT ["jupyter", \
            "notebook", \
            "--no-browser", \
            "--ip='*'", \
            "--port=8888", \
            "--notebook-dir=/notebooks" \
            ]
