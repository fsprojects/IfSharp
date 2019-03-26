FROM mono:5.18.0.225

# Install Anaconda (conda / miniconda)
RUN apt update \
	&& apt install -y \
		apt-transport-https \
		wget
RUN wget https://repo.anaconda.com/miniconda/Miniconda3-latest-Linux-x86_64.sh -O ~/Miniconda3-latest-Linux-x86_64.sh
RUN chmod u+x ~/Miniconda3-latest-Linux-x86_64.sh
RUN ~/Miniconda3-latest-Linux-x86_64.sh -b -p /opt/miniconda 
RUN . /opt/miniconda/bin/activate
ENV PATH="/opt/miniconda/bin:$PATH"

# Test conda to ensure the above worked.
RUN conda -V 

# Install other dependencies
RUN apt update \
    && apt install -y \
        python3-pip \
        git

# Install Jupyter and extensions
RUN conda update -n base -c defaults conda
RUN conda install -c anaconda jupyter
RUN conda install -c conda-forge jupyter_contrib_nbextensions
RUN conda install -c conda-forge jupyter_nbextensions_configurator

# Install IfSharp
WORKDIR /
RUN git clone https://github.com/fsprojects/IfSharp.git
RUN mkdir notebooks
VOLUME notebooks

# Add user 
RUN useradd -ms /bin/bash ifsharp-user
RUN chown -R ifsharp-user /notebooks && chown -R ifsharp-user /IfSharp
USER ifsharp-user

WORKDIR /IfSharp
RUN ./build.sh
RUN mono bin/ifsharp.exe --install

# Install extensions and configurator
RUN jupyter contrib nbextension install --user
RUN jupyter nbextensions_configurator enable --user

EXPOSE 8888

# Final entrypoint
ENTRYPOINT ["jupyter", \
            "notebook", \
            "--no-browser", \
            "--ip='0.0.0.0'", \
            "--port=8888", \
            "--notebook-dir=/notebooks" \
            ]
