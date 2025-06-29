#!/bin/bash
set -e

rm -rf refs
refasmer -v --all -O refs -g "originals/**/*.dll"
